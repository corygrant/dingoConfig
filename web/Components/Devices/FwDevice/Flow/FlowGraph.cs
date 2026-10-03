using domain.Interfaces;
using domain.Models;
using FwDeviceModel = domain.Devices.FwDevice;

namespace web.Components.Devices.FwDevice.Flow;

// DTOs sent to the flow editor (serialized camelCase by JS interop)
public sealed record FlowHandleDto(string Id, string Label, string[] DataTypes, string? Var, bool Connected);

public sealed record FlowNodeDto(
    string Id,
    string Category,
    string Label,
    string Subtitle,
    double X,
    double Y,
    bool Enabled,
    bool Deletable,
    List<FlowHandleDto> Inputs,
    List<FlowHandleDto> Outputs);

public sealed record FlowEdgeDto(string Id, string Source, string SourceHandle, string Target, string TargetHandle, string Var);

public sealed record FlowGraphDto(List<FlowNodeDto> Nodes, List<FlowEdgeDto> Edges);

// Payloads received from the flow editor
public sealed record FlowNodeMove(string Id, double X, double Y);

public sealed record FlowEdgeRef(string Target, string TargetHandle);

/// <summary>
/// Projects an FwDevice's function config onto a node graph and applies graph edits back to it.
/// The device config is the single source of truth: a node is an enabled function slot and an
/// edge is a function input set to another function's var map index. Only node positions are
/// stored separately, in FwDevice.FlowLayout.
/// </summary>
public sealed class FlowGraph
{
    public const string DeviceNodeId = "device";
    private const string InputPrefix = "in:";
    private const string OutputPrefix = "out:";
    private const double ColumnWidth = 360;
    private const double NodeGap = 40;

    public sealed record Slot(FlowNodeType Type, IDeviceFunction Function)
    {
        public string Id => Type.NodeId(Function);
        public string Label => Type.SlotLabel(Function);
        public bool Enabled => Type.GetEnabled(Function);
    }

    private readonly FwDeviceModel _device;
    private readonly List<Slot> _slots = [];
    private readonly Dictionary<string, Slot> _slotsById = [];
    private readonly Dictionary<IDeviceFunction, Slot> _slotsByFunction = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IDeviceFunction, List<DeviceVariable>> _varsByOwner = new(ReferenceEqualityComparer.Instance);
    private readonly List<DeviceVariable> _deviceVars = [];
    private readonly Dictionary<int, DeviceVariable> _vars = [];

    public FlowGraph(FwDeviceModel device)
    {
        _device = device;

        foreach (var type in FlowNodeTypes.All)
        {
            foreach (var function in type.Slots(device))
            {
                var slot = new Slot(type, function);
                _slots.Add(slot);
                _slotsById[slot.Id] = slot;
                _slotsByFunction[function] = slot;
            }
        }

        foreach (var variable in device.VarMap)
        {
            _vars[variable.VariableIndex] = variable;

            if (variable.Owner is null)
            {
                if (variable.VariableIndex != 0) // 0 = "None", i.e. not connected
                    _deviceVars.Add(variable);
            }
            else
            {
                if (!_varsByOwner.TryGetValue(variable.Owner, out var list))
                    _varsByOwner[variable.Owner] = list = [];
                list.Add(variable);
            }
        }
    }

    public Slot? Find(string nodeId) => _slotsById.GetValueOrDefault(nodeId);

    /// <summary>Disabled slots grouped by type, i.e. what can be added to the canvas.</summary>
    public IEnumerable<(FlowNodeType Type, List<Slot> Free)> FreeSlots() =>
        _slots.Where(s => !s.Enabled)
            .GroupBy(s => s.Type)
            .Select(g => (g.Key, g.ToList()));

    public FlowGraphDto Build()
    {
        // Enabled slots are nodes. Disabled slots still referenced by an enabled input are shown
        // too (dimmed) so the connection isn't hidden.
        var visible = _slots.Where(s => s.Enabled).ToList();
        var visibleIds = visible.Select(s => s.Id).ToHashSet();

        foreach (var (_, inputs) in AllInputs(visible.ToList()))
        {
            foreach (var input in inputs)
            {
                if (SourceSlot(input.Get()) is { } source && visibleIds.Add(source.Id))
                    visible.Add(source);
            }
        }

        visible = visible.OrderBy(s => _slots.IndexOf(s)).ToList();
        visibleIds.Add(DeviceNodeId);

        var edges = new List<FlowEdgeDto>();
        var connectedOutputs = new HashSet<int>();
        var connectedInputs = new HashSet<string>();

        foreach (var (id, inputs) in AllInputs(visible))
        {
            foreach (var input in inputs)
            {
                var index = input.Get();
                if (index == 0 || !_vars.TryGetValue(index, out var variable))
                    continue;

                var sourceId = variable.Owner is null ? DeviceNodeId : SourceSlot(index)?.Id;
                if (sourceId is null || !visibleIds.Contains(sourceId))
                    continue;

                var targetHandle = InputPrefix + input.Key;
                edges.Add(new FlowEdgeDto($"{id}:{input.Key}", sourceId, OutputPrefix + index,
                    id, targetHandle, index.ToString()));
                connectedOutputs.Add(index);
                connectedInputs.Add($"{id}:{targetHandle}");
            }
        }

        var nodes = new List<FlowNodeDto>
        {
            new(DeviceNodeId, nameof(FlowCategory.Device), _device.Name, _device.Def.TypeName, 0, 0,
                Enabled: true, Deletable: false,
                Inputs: DeviceInputs()
                    .Select(i => new FlowHandleDto(InputPrefix + i.Key, i.Label, i.DataTypes, null,
                        connectedInputs.Contains($"{DeviceNodeId}:{InputPrefix}{i.Key}")))
                    .ToList(),
                Outputs: _deviceVars.Select(v => OutputHandle(v, v.GetName(), connectedOutputs)).ToList())
        };

        foreach (var slot in visible)
        {
            var outputs = _varsByOwner.GetValueOrDefault(slot.Function, [])
                .Select(v => OutputHandle(v, OutputLabel(slot.Function, v), connectedOutputs))
                .ToList();

            var inputs = slot.Type.Inputs(slot.Function)
                .Select(i => new FlowHandleDto(InputPrefix + i.Key, i.Label, i.DataTypes, null,
                    connectedInputs.Contains($"{slot.Id}:{InputPrefix}{i.Key}")))
                .ToList();

            nodes.Add(new FlowNodeDto(slot.Id, slot.Type.Category.ToString(), slot.Function.Name, slot.Label,
                0, 0, slot.Enabled, Deletable: true, inputs, outputs));
        }

        return new FlowGraphDto(ApplyLayout(nodes), edges);
    }

    /// <summary>Live values keyed by var map index, for the value badges and active edges.</summary>
    public Dictionary<string, object?> Values()
    {
        var values = new Dictionary<string, object?>();

        foreach (var variable in _deviceVars)
            values[variable.VariableIndex.ToString()] = DeviceValue(variable);

        foreach (var slot in _slots)
        {
            if (!_varsByOwner.TryGetValue(slot.Function, out var vars))
                continue;

            var slotValues = slot.Type.Values(slot.Function);
            for (var i = 0; i < vars.Count; i++)
                values[vars[i].VariableIndex.ToString()] = i < slotValues.Length ? Normalize(slotValues[i]) : null;
        }

        return values;
    }

    /// <summary>Sets a function input to a source variable. Returns an error message on failure.</summary>
    public string? Connect(string sourceHandle, string targetId, string targetHandle)
    {
        if (!TryParseVar(sourceHandle, out var index) || !_vars.TryGetValue(index, out var variable))
            return "Unknown source variable";

        if (FindInput(targetId, targetHandle) is not { } input)
            return "Unknown target input";

        if (!input.DataTypes.Contains(variable.DataType))
            return $"{input.Label} accepts {string.Join("/", input.DataTypes)}, not {variable.DataType}";

        input.Set(index);
        return null;
    }

    public void Disconnect(string targetId, string targetHandle)
    {
        FindInput(targetId, targetHandle)?.Set(0);
    }

    /// <summary>Number of inputs (on any function) that use one of this slot's variables.</summary>
    public int CountReferences(Slot slot)
    {
        var indices = VarIndices(slot);
        return AllInputs(_slots).Sum(n => n.Inputs.Count(i => indices.Contains(i.Get())));
    }

    public void Add(Slot slot, FlowNodePosition position)
    {
        slot.Type.SetEnabled(slot.Function, true);
        _device.FlowLayout[slot.Id] = position;
    }

    /// <summary>Disables the slot and disconnects every input that used its variables.</summary>
    public void Remove(Slot slot)
    {
        slot.Type.SetEnabled(slot.Function, false);

        var indices = VarIndices(slot);
        foreach (var (_, inputs) in AllInputs(_slots))
        {
            foreach (var input in inputs)
            {
                if (indices.Contains(input.Get()))
                    input.Set(0);
            }
        }
    }

    public void Move(FlowNodeMove move)
    {
        _device.FlowLayout[move.Id] = new FlowNodePosition(move.X, move.Y);
    }

    private Slot? SourceSlot(int index) =>
        index != 0 && _vars.TryGetValue(index, out var variable) && variable.Owner is not null
            ? _slotsByFunction.GetValueOrDefault(variable.Owner)
            : null;

    private FlowInputDef? FindInput(string nodeId, string handle)
    {
        if (!handle.StartsWith(InputPrefix))
            return null;

        var inputs = nodeId == DeviceNodeId ? DeviceInputs()
            : Find(nodeId) is { } slot ? slot.Type.Inputs(slot.Function) : null;
        return inputs?.FirstOrDefault(i => i.Key == handle[InputPrefix.Length..]);
    }

    private IReadOnlyList<FlowInputDef> DeviceInputs()
    {
        List<FlowInputDef> inputs =
            [new("muteCanTx", "Mute CAN TX", ["bool"], () => _device.MuteCanTxInput, v => _device.MuteCanTxInput = v)];

        if (_device.Def.CanSleep)
            inputs.Add(new("forceSleep", "Force Sleep", ["bool"], () => _device.ForceSleepInput, v => _device.ForceSleepInput = v));

        return inputs;
    }

    // Every node with inputs: function slots plus the device node
    private IEnumerable<(string Id, IReadOnlyList<FlowInputDef> Inputs)> AllInputs(IEnumerable<Slot> slots) =>
        slots.Select(s => (s.Id, s.Type.Inputs(s.Function))).Append((DeviceNodeId, DeviceInputs()));

    private HashSet<int> VarIndices(Slot slot) =>
        _varsByOwner.GetValueOrDefault(slot.Function, []).Select(v => v.VariableIndex).ToHashSet();

    private static bool TryParseVar(string handle, out int index)
    {
        index = 0;
        return handle.StartsWith(OutputPrefix) && int.TryParse(handle[OutputPrefix.Length..], out index);
    }

    private static FlowHandleDto OutputHandle(DeviceVariable variable, string label, HashSet<int> connected) =>
        new(OutputPrefix + variable.VariableIndex, label, [variable.DataType], variable.VariableIndex.ToString(),
            connected.Contains(variable.VariableIndex));

    // Keypad variables are named "keypad1 - button3", label them "button3" rather than "State"
    private static string OutputLabel(IDeviceFunction function, DeviceVariable variable)
    {
        var prefix = function.Name + " - ";
        var name = variable.GetName();
        return name.StartsWith(prefix) ? name[prefix.Length..] : variable.PropertyName;
    }

    private object? DeviceValue(DeviceVariable variable) => variable.GetName() switch
    {
        "Always On" => true,
        "State" => _device.DeviceState.ToString(),
        "Battery Voltage" => Normalize(_device.BatteryVoltage),
        _ => null
    };

    private static object? Normalize(object? value) => value switch
    {
        double d => Math.Round(d, 2),
        float f => Math.Round(f, 2),
        Enum e => e.ToString(),
        _ => value
    };

    /// <summary>
    /// Uses saved positions, and stacks nodes without one at the bottom of a column for their
    /// category (sources | logic | outputs). Assigned positions are saved so they stay stable.
    /// </summary>
    private List<FlowNodeDto> ApplyLayout(List<FlowNodeDto> nodes)
    {
        var columnBottoms = new Dictionary<int, double>();
        var placed = new List<FlowNodeDto>(nodes.Count);
        var unplaced = new List<FlowNodeDto>();

        foreach (var node in nodes)
        {
            if (_device.FlowLayout.TryGetValue(node.Id, out var pos))
            {
                var column = Column(node);
                columnBottoms[column] = Math.Max(columnBottoms.GetValueOrDefault(column, double.MinValue),
                    pos.Y + EstimateHeight(node));
                placed.Add(node with { X = pos.X, Y = pos.Y });
            }
            else
            {
                unplaced.Add(node);
            }
        }

        foreach (var node in unplaced)
        {
            var column = Column(node);
            var y = columnBottoms.TryGetValue(column, out var bottom) ? bottom + NodeGap : 0;
            var pos = new FlowNodePosition(column * ColumnWidth, y);

            _device.FlowLayout[node.Id] = pos;
            columnBottoms[column] = y + EstimateHeight(node);
            placed.Add(node with { X = pos.X, Y = pos.Y });
        }

        return placed;
    }

    private static int Column(FlowNodeDto node) => node.Category switch
    {
        nameof(FlowCategory.Logic) => 1,
        nameof(FlowCategory.Sink) => 2,
        _ => 0
    };

    // Rough rendered height; mirrors the collapse threshold in FunctionNode.jsx
    private static double EstimateHeight(FlowNodeDto node)
    {
        static int Rows(List<FlowHandleDto> handles) =>
            handles.Count > 8 ? handles.Count(h => h.Connected) + 1 : handles.Count;

        return 52 + 22 * (Rows(node.Inputs) + Rows(node.Outputs)) + 16;
    }
}
