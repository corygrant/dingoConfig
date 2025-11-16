using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class OutputPwm(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("number")] public int Number { get; set; } = num;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("softStartEnabled")] public bool SoftStartEnabled { get; set; }
    [JsonPropertyName("variableDutyCycle")] public bool VariableDutyCycle { get; set; }
    [JsonPropertyName("dutyCycleInput")] public VarMap DutyCycleInput { get; set; }
    [JsonPropertyName("fixedDutyCycle")] public int FixedDutyCycle { get; set; }
    [JsonPropertyName("frequency")] public int Frequency { get; set; }
    [JsonPropertyName("softStartRampTime")] public int SoftStartRampTime { get; set; }
    [JsonPropertyName("dutyCycleDenominator")] public int DutyCycleDenominator { get; set; }
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.OutputsPwm);
        data[1] = Convert.ToByte((index & 0x0F) << 4);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 8) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        SoftStartEnabled = Convert.ToBoolean((data[1] & 0x02) >> 1);
        VariableDutyCycle = Convert.ToBoolean((data[1] & 0x04) >> 2);
        DutyCycleInput = (VarMap)data[2];
        Frequency = (data[3] << 1) + (data[4] & 0x01);
        FixedDutyCycle = (data[4] & 0xFE) >> 1;
        SoftStartRampTime = (data[5] << 8) + data[6];
        DutyCycleDenominator = data[7];

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.OutputsPwm);
        data[1] = Convert.ToByte((((Number - 1) & 0x0F) << 4) + ((Convert.ToByte(VariableDutyCycle) & 0x01) << 2) +
                                 ((Convert.ToByte(SoftStartEnabled) & 0x01) << 1) + (Convert.ToByte(Enabled) & 0x01));
        data[2] = Convert.ToByte(DutyCycleInput);
        data[3] = Convert.ToByte(Frequency >> 1);
        data[4] = Convert.ToByte((Frequency & 0x01) +
                                 ((FixedDutyCycle & 0x7F) << 1));
        data[5] = Convert.ToByte(SoftStartRampTime >> 8);
        data[6] = Convert.ToByte(SoftStartRampTime & 0xFF);
        data[7] = Convert.ToByte(DutyCycleDenominator);

        return data;
    }
}