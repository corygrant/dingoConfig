using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class StarterDisable(string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("input")] public VarMap Input {get; set;}
    [JsonPropertyName("output1")] bool Output1 {get; set;}
    [JsonPropertyName("output2")] bool Output2 {get; set;}
    [JsonPropertyName("output3")] bool Output3 {get; set;}
    [JsonPropertyName("output4")] bool Output4 {get; set;}
    [JsonPropertyName("output5")] bool Output5 {get; set;}
    [JsonPropertyName("output6")] bool Output6 {get; set;}
    [JsonPropertyName("output7")] bool Output7 {get; set;}
    [JsonPropertyName("output8")] bool Output8 {get; set;}
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.StarterDisable);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 4) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Input = (VarMap)(data[2]);
        Output1 = Convert.ToBoolean(data[3] & 0x01);
        Output2 = Convert.ToBoolean((data[3] & 0x02) >> 1);
        Output3 = Convert.ToBoolean((data[3] & 0x04) >> 2);
        Output4 = Convert.ToBoolean((data[3] & 0x08) >> 3);
        Output5 = Convert.ToBoolean((data[3] & 0x10) >> 4);
        Output6 = Convert.ToBoolean((data[3] & 0x20) >> 5);
        Output7 = Convert.ToBoolean((data[3] & 0x40) >> 6);
        Output8 = Convert.ToBoolean((data[3] & 0x80) >> 7);

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.StarterDisable);
        data[1] = Convert.ToByte(Convert.ToByte(Enabled) & 0x01);
        data[2] = Convert.ToByte(Input);
        data[3] = Convert.ToByte(((Convert.ToByte(Output8) & 0x01) << 7) +
                                 ((Convert.ToByte(Output7) & 0x01) << 6) +
                                 ((Convert.ToByte(Output6) & 0x01) << 5) +
                                 ((Convert.ToByte(Output5) & 0x01) << 4) +
                                 ((Convert.ToByte(Output4) & 0x01) << 3) +
                                 ((Convert.ToByte(Output3) & 0x01) << 2) +
                                 ((Convert.ToByte(Output2) & 0x01) << 1) +
                                 (Convert.ToByte(Output1) & 0x01));
        return data;
    }
}