using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class Counter(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("number")] public int Number {get; set;} = num;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("incInput")] public VarMap IncInput { get; set; }
    [JsonPropertyName("decInput")] public VarMap DecInput { get; set; }
    [JsonPropertyName("resetInput")] public  VarMap ResetInput { get; set; }
    [JsonPropertyName("minCount")] public int  MinCount {get; set;}
    [JsonPropertyName("maxCount")] public int  MaxCount {get; set;}
    [JsonPropertyName("incEdge")] public InputEdge IncEdge {get; set;}
    [JsonPropertyName("decEdge")] public InputEdge DecEdge {get; set;}
    [JsonPropertyName("resetEdge")] public InputEdge ResetEdge {get; set;}
    [JsonPropertyName("wrapAround")] public bool WrapAround {get; set;}
    
    [JsonIgnore] public int Value {get; set;}
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Counter);
        data[1] = Convert.ToByte(index);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 8) return false;

        Enabled = Convert.ToBoolean(data[2] & 0x01);
        WrapAround = Convert.ToBoolean((data[2] & 0x02) >> 1);
        IncInput = (VarMap)data[3];
        DecInput = (VarMap)data[4];
        ResetInput = (VarMap)data[5];
        MinCount = (data[6] & 0x0F);
        MaxCount = ((data[6] & 0xF0) >> 4);
        IncEdge = (InputEdge)(data[7] & 0x03);
        DecEdge = (InputEdge)((data[7] & 0x0C) >> 2);
        ResetEdge = (InputEdge)((data[7] & 0x30) >> 4);

        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Counter);
        data[1] = Convert.ToByte(Number - 1);
        data[2] = Convert.ToByte(Convert.ToByte(Enabled) +
                                 (Convert.ToByte(WrapAround) << 1));
        data[3] = Convert.ToByte(IncInput);
        data[4] = Convert.ToByte(DecInput);
        data[5] = Convert.ToByte(ResetInput);
        data[6] = Convert.ToByte((MinCount & 0x0F) +
                                 ((MaxCount & 0x0F) << 4));
        data[7] = Convert.ToByte(((int)IncEdge & 0x03) +
                                 (((int)DecEdge & 0x03) << 2) +
                                 (((int)ResetEdge & 0x03) << 4));

        return data;
    }
}