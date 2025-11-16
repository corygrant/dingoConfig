using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class VirtualInput(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("number")] public int Number {get; set;} = num;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("not0")] public bool Not0 {get; set;}
    [JsonPropertyName("var0")] public VarMap Var0 { get; set; }
    [JsonPropertyName("cond0")] public Conditional Cond0 { get; set; }
    [JsonPropertyName("not1")] public bool Not1 {get; set;}
    [JsonPropertyName("var1")] public VarMap Var1 { get; set; }
    [JsonPropertyName("cond1")] public Conditional Cond1 { get; set; }
    [JsonPropertyName("not2")] public bool Not2 {get; set;}
    [JsonPropertyName("var2")] public VarMap Var2 { get; set; }
    [JsonPropertyName("cond2")] public Conditional Cond2 { get; set; }
    [JsonPropertyName("mode")] public InputMode Mode {get; set;}
    
    [JsonIgnore] public bool Value {get; set;}
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.VirtualInputs);
        data[1] = Convert.ToByte(index);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 7) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Not0 = Convert.ToBoolean((data[1] & 0x02) >> 1);
        Not1 = Convert.ToBoolean((data[1] & 0x04) >> 2);
        Not2 = Convert.ToBoolean((data[1] & 0x08) >> 3);
        Var0 = (VarMap)data[3];
        Var1 = (VarMap)data[4];
        Var2 = (VarMap)data[5];
        Mode = (InputMode)((data[6] & 0xC0) >> 6);
        Cond0 = (Conditional)(data[6] & 0x03);
        Cond1 = (Conditional)((data[6] & 0x0C) >> 2);

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.VirtualInputs);
        data[1] = Convert.ToByte((Convert.ToByte(Not2) << 3) +
                                 (Convert.ToByte(Not1) << 2) +
                                 (Convert.ToByte(Not0) << 1) +
                                 Convert.ToByte(Enabled));
        data[2] = Convert.ToByte(Number - 1);
        data[3] = Convert.ToByte(Var0);
        data[4] = Convert.ToByte(Var1);
        data[5] = Convert.ToByte(Var2);
        data[6] = Convert.ToByte(((Convert.ToByte(Mode) & 0x03) << 0x06) +
                                 ((Convert.ToByte(Cond1) & 0x03) << 2) +
                                 (Convert.ToByte(Cond0) & 0x03));
        return data;
    }
}