using System.Text.Json.Serialization;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdm.Enums;
using Microsoft.Extensions.Logging;

namespace domain.Devices.dingoPdmMax;

public class PdmMaxDevice : PdmDevice
{
    [JsonIgnore] protected override int MinMajorVersion { get; } = 0;
    [JsonIgnore] protected override int MinMinorVersion { get; } = 4;
    [JsonIgnore] protected override int MinBuildVersion { get; } = 7;

    [JsonIgnore] protected override int NumOutputs { get; } = 4;

    [JsonIgnore] protected override int PdmType { get; } = 1; //0=dingoPDM, 1=dingoPDM-Max

    [JsonIgnore] public override string Type => "dingoPDM-Max";

    /// <summary>
    /// Parameterless constructor for JSON deserialization
    /// </summary>
    [JsonConstructor]
    public PdmMaxDevice() : base()
    {
    }

    /// <summary>
    /// Constructor for programmatic device creation with dependency injection
    /// </summary>
    public PdmMaxDevice(ILogger<PdmMaxDevice> logger, string name, int baseId) : base(logger, name, baseId)
    {
    }

    protected override void ReadMessage2(byte[] data)
    {
        //Unused with Max
    }
    protected override void ReadMessage3(byte[] data)
    {
        Outputs[0].State = (OutState)((data[0] & 0x0F));
        Outputs[1].State = (OutState)(data[0] >> 4);
        Outputs[2].State = (OutState)((data[1] & 0x0F));
        Outputs[3].State = (OutState)(data[1] >> 4);
        
        Wipers.SlowState = Convert.ToBoolean(data[4] & 0x01);
        Wipers.FastState = Convert.ToBoolean((data[4] >> 1) & 0x01);
        Wipers.State = (WiperState)(data[5] >> 4);
        Wipers.Speed = (WiperSpeed)(data[5] & 0x0F);

        Flashers[0].Value = Convert.ToBoolean(data[6] & 0x01) && Flashers[0].Enabled;
        Flashers[1].Value = Convert.ToBoolean((data[6] >> 1) & 0x01) && Flashers[1].Enabled;
        Flashers[2].Value = Convert.ToBoolean((data[6] >> 2) & 0x01) && Flashers[2].Enabled;
        Flashers[3].Value = Convert.ToBoolean((data[6] >> 3) & 0x01) && Flashers[3].Enabled;
    }

    protected override void ReadMessage4(byte[] data)
    {
        Outputs[0].ResetCount = data[0];
        Outputs[1].ResetCount = data[1];
        Outputs[2].ResetCount = data[2];
        Outputs[3].ResetCount = data[3];
    }

    protected override void ReadMessage15(byte[] data)
    {
        Outputs[0].CurrentDutyCycle = data[0];
        Outputs[1].CurrentDutyCycle = data[1];
        Outputs[2].CurrentDutyCycle = data[2];
        Outputs[3].CurrentDutyCycle = data[3];
    }
}