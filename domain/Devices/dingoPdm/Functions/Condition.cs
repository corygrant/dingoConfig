using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class Condition(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("number")] public int Number {get; set;} = num;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("input")] public VarMap Input { get; set; }
    [JsonPropertyName("operator")] public Operator Operator {get; set;}
    [JsonPropertyName("arg")] public int Arg {get; set;}
    
    [JsonIgnore] public int Value {get; set;}
    
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Conditions);
        data[1] = Convert.ToByte(index);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 6) return false;

        Enabled = Convert.ToBoolean(data[2] & 0x01);
        Operator = (Operator)(data[2] >> 4);
        Input = (VarMap)(data[3]);
        Arg = (data[4] << 8) + data[5];

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Conditions);
        data[1] = Convert.ToByte(Number - 1);
        data[2] = Convert.ToByte((Convert.ToByte(Operator) << 4) +
                                 (Convert.ToByte(Enabled)));
        data[3] = Convert.ToByte(Input);
        data[4] = Convert.ToByte(Arg >> 8);
        data[5] = Convert.ToByte(Arg & 0xFF);

        return data;
    }
}