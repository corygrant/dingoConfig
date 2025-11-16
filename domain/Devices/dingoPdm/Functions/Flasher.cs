using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class Flasher(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("number")] public int Number {get; set;} = num;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("single")] public bool Single {get; set;}
    [JsonPropertyName("input")] public VarMap Input {get; set;}
    [JsonPropertyName("onTime")] public int OnTime {get; set;}
    [JsonPropertyName("offTime")] public int  OffTime {get; set;}
    
    [JsonIgnore] public bool Value {get; set;}
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Flashers);
        data[1] = Convert.ToByte((index & 0x0F) << 4);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 6) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Single = Convert.ToBoolean((data[1] & 0x02) >> 1);
        Input = (VarMap)(data[2]);
        OnTime = (int)(data[4] / 10.0);
        OffTime = (int)(data[5] / 10.0);

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Flashers);
        data[1] = Convert.ToByte((((Number - 1) & 0x0F) << 4) +
                                 (Convert.ToByte(Single) << 1) +
                                 (Convert.ToByte(Enabled)));
        data[2] = Convert.ToByte(Input);
        data[3] = 0;
        data[4] = Convert.ToByte(OnTime * 10);
        data[5] = Convert.ToByte(OffTime * 10);
        return data;
    }
}