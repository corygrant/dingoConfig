import { memo, useEffect, useState } from 'react';
import { Handle, Position, useUpdateNodeInternals } from '@xyflow/react';
import { useEditor, useLiveValue } from './context.js';

// Nodes with more handles than this only show connected ones until expanded.
const COLLAPSE_THRESHOLD = 8;

function typeClass(dataTypes) {
  return dataTypes.length === 1 ? `df-type-${dataTypes[0]}` : 'df-type-any';
}

function formatValue(value, dataType) {
  if (value === undefined || value === null) return null;
  if (typeof value === 'boolean') return value ? 'ON' : 'OFF';
  if (typeof value === 'number') return dataType === 'float' ? value.toFixed(2) : String(Math.round(value));
  return String(value);
}

function ValueBadge({ handle }) {
  const value = useLiveValue(handle.var);
  const text = formatValue(value, handle.dataTypes[0]);
  if (text === null) return null;
  const cls = typeof value === 'boolean' ? (value ? 'df-badge df-badge-on' : 'df-badge df-badge-off') : 'df-badge';
  return <span className={cls}>{text}</span>;
}

function useCollapsible(handles) {
  const [expanded, setExpanded] = useState(false);
  const collapsible = handles.length > COLLAPSE_THRESHOLD;
  const visible = collapsible && !expanded ? handles.filter((h) => h.connected) : handles;
  return { collapsible, expanded, setExpanded, visible };
}

function ExpandToggle({ state, total, noun }) {
  if (!state.collapsible) return null;
  return (
    <button
      type="button"
      className="df-expand nodrag"
      onClick={() => state.setExpanded((e) => !e)}
    >
      {state.expanded ? `Show connected ${noun}` : `Show all ${noun} (${total})`}
    </button>
  );
}

// Material "settings" icon
const gearPath =
  'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z';

function PropertiesButton({ id }) {
  const { propertiesNodeId, openProperties } = useEditor();
  const open = propertiesNodeId === id;
  return (
    <button
      type="button"
      className={`df-gear nodrag${open ? ' df-gear-open' : ''}`}
      title="Properties"
      onClick={(e) => {
        e.stopPropagation();
        openProperties(id);
      }}
    >
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path d={gearPath} /></svg>
    </button>
  );
}

function FunctionNode({ id, data, selected }) {
  const updateNodeInternals = useUpdateNodeInternals();
  const inputs = useCollapsible(data.inputs);
  const outputs = useCollapsible(data.outputs);

  const handleKey = inputs.visible.map((h) => h.id).join() + '|' + outputs.visible.map((h) => h.id).join();
  useEffect(() => updateNodeInternals(id), [id, handleKey, updateNodeInternals]);

  const classes = [
    'df-node',
    `df-cat-${data.category}`,
    data.enabled ? '' : 'df-disabled',
    selected ? 'df-selected' : '',
  ].join(' ');

  return (
    <div className={classes}>
      <div className="df-header">
        <div className="df-header-text">
          <div className="df-title" title={data.label}>{data.label}</div>
          <div className="df-subtitle">
            {data.subtitle}
            {!data.enabled && <span className="df-tag">disabled</span>}
          </div>
        </div>
        {data.category !== 'Device' && <PropertiesButton id={id} />}
      </div>

      {outputs.visible.length > 0 || outputs.collapsible ? (
        <div className="df-section">
          {outputs.visible.map((h) => (
            <div className="df-row df-row-out" key={h.id}>
              <ValueBadge handle={h} />
              <span className="df-label" title={h.label}>{h.label}</span>
              <Handle type="source" position={Position.Right} id={h.id} className={`df-handle ${typeClass(h.dataTypes)}`} />
            </div>
          ))}
          <ExpandToggle state={outputs} total={data.outputs.length} noun="outputs" />
        </div>
      ) : null}

      {inputs.visible.length > 0 || inputs.collapsible ? (
        <div className="df-section">
          {inputs.visible.map((h) => (
            <div className="df-row df-row-in" key={h.id}>
              <Handle type="target" position={Position.Left} id={h.id} className={`df-handle ${typeClass(h.dataTypes)}`} />
              <span className="df-label" title={h.label}>{h.label}</span>
            </div>
          ))}
          <ExpandToggle state={inputs} total={data.inputs.length} noun="inputs" />
        </div>
      ) : null}
    </div>
  );
}

export default memo(FunctionNode);
