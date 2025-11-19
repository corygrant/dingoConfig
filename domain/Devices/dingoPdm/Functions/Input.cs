using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm.Functions;

public class Input(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("number")] public int Number { get; set; } = num;
    [JsonPropertyName("state")] public bool State { get; set; }
    [JsonPropertyName("invert")] public bool Invert { get; set; }
    [JsonPropertyName("mode")] public InputMode Mode { get; set; }
    [JsonPropertyName("debounceTime")] public int DebounceTime { get; set; }
    [JsonPropertyName("pull")] public InputPull Pull { get; set; }
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return (data & 0xF0) >> 4;
    }
    
    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Inputs) return null;
        
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Inputs, 0, 8);
        InsertSignalInt(data, Number - 1, 12, 4);

        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Inputs,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 2,
                Payload = data
            },
            MsgDescription = $"Input{Number}"
        };
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Inputs) return null;
        
        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Inputs,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 4,
                Payload = Write()
            },
            MsgDescription = $"Input{Number}"
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.Inputs) return false;
        if (data.Length != 4) return false;

        Enabled = ExtractSignalInt(data, 8, 1) == 1;
        Mode = (InputMode)ExtractSignalInt(data, 9, 2);
        Invert = ExtractSignalInt(data, 11, 1) == 1;
        DebounceTime = (int)ExtractSignal(data, 16, 8, factor: 10.0);
        Pull = (InputPull)ExtractSignalInt(data, 24, 2);

        return true;
    }

    private byte[] Write()
    {
        byte[] data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Inputs, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertSignalInt(data, (long)Mode, 9, 2);
        InsertBool(data, Invert, 11);
        InsertSignalInt(data, Number - 1, 12, 4);
        InsertSignal(data, DebounceTime, 16, 8, factor: 10.0);
        InsertSignalInt(data, (long)Pull, 24, 2);
        return data;
    }
}