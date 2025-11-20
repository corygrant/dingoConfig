using contracts.Devices.Pdm.Functions;

namespace contracts.Devices.Pdm;

public class PdmStateDto : DeviceStateDto
{
    public int State { get; set; }
    public double TotalCurrent { get; set; }
    public double BatteryVoltage { get; set; }
    public double BoardTempC { get; set; }
    public double BoardTempF { get; set; }
    
    public List<CanInputStateDto> CanInputs { get; set; } = [];
    public List<ConditionStateDto> Conditions { get; set; } = [];
    public List<CounterStateDto> Counters { get; set; } = [];
    public List<FlasherStateDto> Flashers { get; set; } = [];
    public List<InputStateDto> Inputs { get; set; } = [];
    public List<OutputStateDto> Outputs { get; set; } = [];
    //StarterDisable has no state
    public List<VirtualInputStateDto> VirtualInputs { get; set; } = [];
    public WiperStateDto? Wipers { get; set; }
}