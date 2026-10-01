// uPlot interop for dashboard sparklines and the application plot.
// .NET owns the sample history and sends incremental chunks: { generation, seq, reset, times[], values[][] },
// where values[i] belongs to keys[i] and null is a gap.
import uPlot from '../../lib/uplot/uPlot.esm.js';

const stepped = uPlot.paths.stepped({ align: 1 });

// uPlot's default legend time only shows minutes; samples are 50 ms apart
const fmtCursorTime = uPlot.fmtDate('{HH}:{mm}:{ss}.{fff}');

function cssVar(name, fallback) {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || fallback;
}

function theme() {
    return {
        primary: cssVar('--mud-palette-primary', '#7fff00'),
        primaryRgb: cssVar('--mud-palette-primary-rgb', '127,255,0'),
        text: cssVar('--mud-palette-text-secondary', 'rgba(255,255,255,0.7)'),
        grid: cssVar('--mud-palette-lines-default', 'rgba(255,255,255,0.12)'),
    };
}

// Pads the y-range so flat lines and 0/1 states aren't drawn on the plot edge
function paddedRange(u, min, max) {
    if (min == null || max == null) return [0, 1];
    if (min === max) return [min - 1, max + 1];
    const pad = (max - min) * 0.1;
    return [min - pad, max + pad];
}

/** Aligned time series: times[] plus one values[] per key, all the same length. */
class SeriesBuffer {
    constructor(capacity) {
        this.capacity = capacity;
        this.keys = [];
        this.times = [];
        this.values = [];
    }

    apply(keys, chunk) {
        if (chunk.reset || keys.length !== this.keys.length || keys.some((k, i) => k !== this.keys[i])) {
            this.keys = keys.slice();
            this.times = [];
            this.values = keys.map(() => []);
        }

        for (let i = 0; i < chunk.times.length; i++) this.times.push(chunk.times[i]);
        this.values.forEach((arr, k) => {
            const incoming = chunk.values[k];
            for (let i = 0; i < incoming.length; i++) arr.push(incoming[i]);
        });

        const excess = this.times.length - this.capacity;
        if (excess > 0) {
            this.times.splice(0, excess);
            this.values.forEach(arr => arr.splice(0, excess));
        }
    }

    series(key) {
        return this.values[this.keys.indexOf(key)] ?? [];
    }
}

// ---------------------------------------------------------------------------------------------
// Sparklines: many tiny single-value charts fed from one shared buffer, one interop call per tick
// ---------------------------------------------------------------------------------------------

class SparklineGroup {
    constructor(capacity, windowSeconds) {
        this.buffer = new SeriesBuffer(capacity);
        this.windowSeconds = windowSeconds;
        this.charts = new Map(); // id -> { plot, key }
    }

    /** charts: full chart list when it changed since the last call, otherwise null */
    update(charts, keys, chunk) {
        if (charts) this.#setCharts(charts);
        this.buffer.apply(keys, chunk);

        for (const { plot, key } of this.charts.values())
            plot.setData([this.buffer.times, this.buffer.series(key)]);
    }

    #setCharts(charts) {
        const wanted = new Map(charts.map(c => [c.id, c.key]));
        for (const [id, chart] of this.charts) {
            if (wanted.get(id) !== chart.key) {
                chart.plot.destroy();
                this.charts.delete(id);
            }
        }

        const t = theme();
        for (const c of charts) {
            if (this.charts.has(c.id)) continue;
            c.el.replaceChildren();

            const plot = new uPlot({
                width: c.width,
                height: c.height,
                pxAlign: false,
                cursor: { show: false },
                select: { show: false },
                legend: { show: false },
                scales: {
                    x: { time: false, range: (u, min, max) => [max - this.windowSeconds, max] },
                    y: { range: paddedRange },
                },
                axes: [{ show: false }, { show: false }],
                series: [
                    {},
                    {
                        stroke: t.primary,
                        fill: `rgba(${t.primaryRgb},0.12)`,
                        width: 1.5,
                        paths: c.discrete ? stepped : undefined,
                        points: { show: false },
                    },
                ],
            }, [[], []], c.el);

            this.charts.set(c.id, { plot, key: c.key });
        }
    }

    dispose() {
        for (const { plot } of this.charts.values()) plot.destroy();
        this.charts.clear();
    }
}

export function createSparklineGroup(capacity, windowSeconds) {
    return new SparklineGroup(capacity, windowSeconds);
}

// ---------------------------------------------------------------------------------------------
// Application plot: one stacked pane per unit, x-axis zoom and cursor synced across panes
// ---------------------------------------------------------------------------------------------

let plotCount = 0;

class AppPlot {
    constructor(container, dotNet) {
        this.container = container;
        this.dotNet = dotNet;
        this.syncKey = `app-plot-${++plotCount}`;
        this.buffer = new SeriesBuffer(0);
        this.traces = [];
        this.panes = []; // { plot, traces }
        this.windowSeconds = 60;
        this.follow = true;
        this.userRange = null;
        this.lastSelectAt = 0;
        this.laidOut = null; // container size of the last layout

        this.resizeObserver = new ResizeObserver(() => this.#layout());
        this.resizeObserver.observe(container);
    }

    /**
     * Rebuilds the chart for a new set of traces.
     * traces: [{ key, label, unit, color, discrete }]
     */
    setTraces(traces, capacity, chunk) {
        this.traces = traces;
        this.buffer = new SeriesBuffer(capacity);
        this.buffer.apply(traces.map(t => t.key), chunk);
        this.#build();
    }

    push(chunk) {
        this.buffer.apply(this.traces.map(t => t.key), chunk);
        this.#redraw();
    }

    /** seconds: visible window while following, 0 = everything recorded */
    setWindow(seconds) {
        this.windowSeconds = seconds;
        this.#redraw();
    }

    setFollow(follow) {
        this.#setFollow(follow, false);
        this.#redraw();
    }

    #setFollow(follow, notify) {
        if (this.follow === follow) return;
        this.follow = follow;
        if (follow) this.userRange = null;
        if (notify) this.dotNet.invokeMethodAsync('OnFollowChanged', follow);
    }

    #build() {
        for (const pane of this.panes) pane.plot.destroy();
        this.panes = [];
        this.container.replaceChildren();

        // Group by unit so every pane has a single, meaningful y-axis
        const groups = new Map();
        for (const t of this.traces) {
            const unit = t.unit || '';
            if (!groups.has(unit)) groups.set(unit, []);
            groups.get(unit).push(t);
        }

        const t = theme();
        const axisBase = {
            stroke: t.text,
            grid: { stroke: t.grid, width: 1 },
            ticks: { stroke: t.grid, width: 1 },
        };

        let i = 0;
        for (const [unit, traces] of groups) {
            const isLast = ++i === groups.size;
            const el = document.createElement('div');
            el.className = 'app-plot-pane';
            this.container.appendChild(el);

            const plot = new uPlot({
                width: Math.max(this.container.clientWidth, 100),
                height: 150,
                cursor: {
                    sync: { key: this.syncKey, setSeries: false },
                    drag: { x: true, y: false, setScale: false },
                },
                scales: {
                    x: { time: true },
                    y: { range: paddedRange },
                },
                axes: [
                    { ...axisBase, show: isLast },
                    { ...axisBase, label: unit || 'State', size: 60, labelSize: 20 },
                ],
                series: [
                    { value: (u, v) => v == null ? '--' : fmtCursorTime(new Date(v * 1000)) },
                    ...traces.map(tr => ({
                        label: tr.label,
                        stroke: tr.color,
                        width: 2,
                        paths: tr.discrete ? stepped : undefined,
                        points: { show: false },
                        value: (u, v) => v == null ? '--' : (tr.discrete ? v.toFixed(0) : v.toFixed(2)),
                    })),
                ],
                hooks: {
                    setSelect: [u => this.#onSelect(u)],
                },
            }, this.#paneData(traces), el);

            plot.over.addEventListener('dblclick', () => {
                this.#setFollow(true, true);
                this.#redraw();
            });

            this.panes.push({ plot, traces });
        }

        this.#layout();
        this.#redraw();
        // Blazor may still be re-rendering the controls above the chart; settle the size once it has
        requestAnimationFrame(() => this.#layout());
    }

    #paneData(traces) {
        return [this.buffer.times, ...traces.map(t => this.buffer.series(t.key))];
    }

    // Drag-select zooms every pane and stops following live data.
    // Synced panes receive the same selection right after the source pane; only the first one counts.
    #onSelect(u) {
        if (u.select.width < 2) return;

        const now = performance.now();
        if (now - this.lastSelectAt > 100) {
            this.lastSelectAt = now;
            this.userRange = {
                min: u.posToVal(u.select.left, 'x'),
                max: u.posToVal(u.select.left + u.select.width, 'x'),
            };
            this.#setFollow(false, true);
            this.#redraw();
        }

        u.setSelect({ left: 0, top: 0, width: 0, height: 0 }, false);
    }

    #xRange() {
        const times = this.buffer.times;
        if (!this.follow && this.userRange) return this.userRange;
        if (times.length === 0) return null;

        const last = times[times.length - 1];
        const first = times[0];
        return this.windowSeconds > 0
            ? { min: Math.max(first, last - this.windowSeconds), max: last }
            : { min: first, max: last };
    }

    #redraw() {
        if (this.laidOut?.width !== this.container.clientWidth || this.laidOut?.height !== this.container.clientHeight)
            this.#layout();

        const range = this.#xRange();
        for (const { plot, traces } of this.panes) {
            plot.batch(() => {
                plot.setData(this.#paneData(traces), false);
                if (range && range.max > range.min) plot.setScale('x', range);
            });
        }
    }

    #layout() {
        const n = this.panes.length;
        if (n === 0) return;

        const width = this.container.clientWidth;
        const height = this.container.clientHeight;
        this.laidOut = { width, height };
        for (const { plot } of this.panes) {
            const legend = plot.root.querySelector('.u-legend');
            const legendHeight = legend ? legend.offsetHeight : 0;
            plot.setSize({ width, height: Math.max(80, Math.floor(height / n) - legendHeight - 4) });
        }
    }

    dispose() {
        this.resizeObserver.disconnect();
        for (const pane of this.panes) pane.plot.destroy();
        this.panes = [];
    }
}

export function createAppPlot(container, dotNet) {
    return new AppPlot(container, dotNet);
}
