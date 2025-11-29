using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm.Functions;

public class Counter(int number, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set; } = name;
    [JsonPropertyName("number")] public int Number {get;} = number;
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
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return data;
    }
    
    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Counter) return null;
        
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Counter, 0, 8);
        InsertSignalInt(data, Number - 1, 8, 8);

        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Counter,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 2,
                Payload = data
            },
            MsgDescription = $"Counter{Number}"
        };
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Counter) return null;
        
        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Counter,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 8,
                Payload = Write()
            },
            MsgDescription = $"Counter{Number}"
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Counter) return false;
        if (data.Length != 8) return false;

        Enabled = ExtractSignalInt(data, 16, 1) == 1;
        WrapAround = ExtractSignalInt(data, 17, 1) == 1;
        IncInput = (VarMap)ExtractSignalInt(data, 24, 8);
        DecInput = (VarMap)ExtractSignalInt(data, 32, 8);
        ResetInput = (VarMap)ExtractSignalInt(data, 40, 8);
        MinCount = (int)ExtractSignalInt(data, 48, 4);
        MaxCount = (int)ExtractSignalInt(data, 52, 4);
        IncEdge = (InputEdge)ExtractSignalInt(data, 56, 2);
        DecEdge = (InputEdge)ExtractSignalInt(data, 58, 2);
        ResetEdge = (InputEdge)ExtractSignalInt(data, 60, 2);

        return true;
    }

    private byte[] Write()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Counter, 0, 8);
        InsertSignalInt(data, Number - 1, 8, 8);
        InsertBool(data, Enabled, 16);
        InsertBool(data, WrapAround, 17);
        InsertSignalInt(data, (long)IncInput, 24, 8);
        InsertSignalInt(data, (long)DecInput, 32, 8);
        InsertSignalInt(data, (long)ResetInput, 40, 8);
        InsertSignalInt(data, MinCount, 48, 4);
        InsertSignalInt(data, MaxCount, 52, 4);
        InsertSignalInt(data, (long)IncEdge, 56, 2);
        InsertSignalInt(data, (long)DecEdge, 58, 2);
        InsertSignalInt(data, (long)ResetEdge, 60, 2);

        return data;
    }
}