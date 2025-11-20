using contracts.Devices.Pdm.Functions;

namespace contracts.Devices.Pdm;

public class PdmConfigDto
{
    public bool SleepEnabled { get; set; }
    public bool CanFiltersEnabled { get; set; }
    
    public List<CanInputConfigDto> CanInputs { get; set; } = [];
    public List<ConditionConfigDto> Conditions { get; set; } = [];
    public List<CounterConfigDto> Counters { get; set; } = [];
    public List<FlasherConfigDto> Flashers { get; set; } = [];
    public List<InputConfigDto> Inputs { get; set; } = [];
    public List<OutputConfigDto> Outputs { get; set; } = [];
    public StarterDisableConfigDto? StarterDisable { get; set; }
    public List<VirtualInputConfigDto> VirtualInputs { get; set; } = [];
    public WiperConfigDto? Wipers { get; set; }
}