using System.Text.Json.Serialization;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdm.Enums;

namespace domain.Devices.dingoPdmMax;

public class PdmMaxDevice(string name, int baseId) : PdmDevice(name, baseId)
{
    protected override int MinMajorVersion { get; } = 0;
    protected override int MinMinorVersion { get; } = 4;
    protected override int MinBuildVersion { get; } = 7;

    protected override int NumOutputs { get; } = 4;
    
    [JsonIgnore] public override string Type => "dingoPDM-Max";

    protected override void SetLimits()
    {
        Outputs[0].NominalCurrentLimit = 26.0;
        Outputs[1].NominalCurrentLimit = 26.0;
        Outputs[2].NominalCurrentLimit = 26.0;
        Outputs[3].NominalCurrentLimit = 26.0;
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