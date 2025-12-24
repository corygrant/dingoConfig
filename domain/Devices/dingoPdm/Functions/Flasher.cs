using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm.Functions;

public class Flasher(int number, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set; } = name;
    [JsonPropertyName("number")] public int Number {get;} = number;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("single")] public bool Single {get; set;}
    [JsonPropertyName("input")] public VarMap Input {get; set;}
    [JsonPropertyName("onTime")] public int OnTime {get; set;}
    [JsonPropertyName("offTime")] public int  OffTime {get; set;}

    [JsonIgnore] public bool Value {get; set;}
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return (data & 0xF0) >> 4;
    }
    
    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Flashers) return null;
        
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Flashers, 0, 8);
        InsertSignalInt(data, Number - 1, 12, 4);

        return new DeviceCanFrame
        {
            DeviceBaseId = baseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Flashers,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 2,
                Payload = data
            },
            MsgDescription = $"Flasher{Number}"
        };
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Flashers) return null;
        
        return new DeviceCanFrame
        {
            DeviceBaseId = baseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Flashers,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 6,
                Payload = Write()
            },
            MsgDescription = $"Flasher{Number}"
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Flashers) return false;
        if (data.Length != 6) return false;

        Enabled = ExtractSignalInt(data, 8, 1) == 1;
        Single = ExtractSignalInt(data, 9, 1) == 1;
        Input = (VarMap)ExtractSignalInt(data, 16, 8);
        OnTime = (int)ExtractSignal(data, 32, 8, factor: 0.1);
        OffTime = (int)ExtractSignal(data, 40, 8, factor: 0.1);

        return true;
    }

    private byte[] Write()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Flashers, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertBool(data, Single, 9);
        InsertSignalInt(data, Number - 1, 12, 4);
        InsertSignalInt(data, (long)Input, 16, 8);
        InsertSignal(data, OnTime, 32, 8, factor: 10.0);
        InsertSignal(data, OffTime, 40, 8, factor: 10.0);
        return data;
    }
}