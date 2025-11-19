using contracts.Devices.Pdm.Functions;

namespace contracts.Devices.Pdm;

public class PdmConfigDto
{
    public string Name { get; set; } = string.Empty;
    public int BaseId { get; set; }
    public bool SleepEnabled { get; set; }
    public bool CanFiltersEnabled { get; set; }
    public int BitRate { get; set; }
    
    public List<InputStateDto> DigitalInputs { get; set; } = [];
    public List<OutputStateDto> Outputs { get; set; } = [];
    public List<CanInputStateDto> CanInputs { get; set; } = [];
    public List<VirtualInputStateDto> VirtualInputs { get; set; } = [];
    public WiperStateDto? Wipers { get; set; }
    public List<FlasherStateDto> Flashers { get; set; } = [];
    public StarterDisableStateDto? StarterDisable { get; set; }
    public List<CounterStateDto> Counters { get; set; } = [];
    public List<ConditionStateDto> Conditions { get; set; } = [];
}