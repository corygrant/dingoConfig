using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class Output(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("number")] public int Number { get; set; } = num;
    [JsonPropertyName("currentLimit")] public int CurrentLimit { get; set; }
    [JsonPropertyName("resetCountLimit")] public int ResetCountLimit { get; set; }
    [JsonPropertyName("resetMode")] public ResetMode ResetMode { get; set; }
    [JsonPropertyName("resetTime")] public double ResetTime { get; set; }
    [JsonPropertyName("inrushCurrentLimit")] public int InrushCurrentLimit { get; set; }
    [JsonPropertyName("inrushTime")] public double InrushTime { get; set; }
    [JsonPropertyName("input")] public VarMap Input { get; set; }
    
    [JsonIgnore] public double Current { get; set; }
    [JsonIgnore] public OutState State { get; set; }
    [JsonIgnore] public int ResetCount { get; set; }
    [JsonIgnore] public double CurrentDutyCycle { get; set; }
    [JsonIgnore] public double CalculatedPower { get; set; }
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Outputs);
        data[1] = Convert.ToByte((index & 0x0F) << 4);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 8) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Input = (VarMap)(data[2]);
        CurrentLimit = data[3];
        ResetCountLimit = (data[4] & 0xF0) >> 4;
        ResetMode = (ResetMode)(data[4] & 0x0F);
        ResetTime = data[5] / 10.0;
        InrushCurrentLimit = data[6];
        InrushTime = data[7] / 10.0;

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Outputs);
        data[1] = Convert.ToByte((((Number - 1) & 0x0F) << 4) + (Convert.ToByte(Enabled) & 0x01));
        data[2] = Convert.ToByte(Input);
        data[3] = Convert.ToByte(CurrentLimit);
        data[4] = Convert.ToByte((Convert.ToByte(ResetCountLimit) << 4) + (Convert.ToByte(ResetMode) & 0x0F));
        data[5] = Convert.ToByte(ResetTime * 10);
        data[6] = Convert.ToByte(InrushCurrentLimit);
        data[7] = Convert.ToByte(InrushTime * 10);
        return data;
    }
}