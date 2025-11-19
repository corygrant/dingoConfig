using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm.Functions;

public class StarterDisable(string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonIgnore] public int Number => 1; // Singleton function
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
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return data;
    }
    
    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.StarterDisable) return null;
        
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.StarterDisable, 0, 8);

        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.StarterDisable,
            Index = 0,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 1,
                Payload = data
            },
            MsgDescription = "StarterDisable"
        };
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.StarterDisable) return null;
        
        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.StarterDisable,
            Index = 0,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 4,
                Payload = Write()
            },
            MsgDescription = "StarterDisable"
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.StarterDisable) return false;
        if (data.Length != 4) return false;

        Enabled = ExtractSignalInt(data, 8, 1) == 1;
        Input = (VarMap)ExtractSignalInt(data, 16, 8);
        Output1 = ExtractSignalInt(data, 24, 1) == 1;
        Output2 = ExtractSignalInt(data, 25, 1) == 1;
        Output3 = ExtractSignalInt(data, 26, 1) == 1;
        Output4 = ExtractSignalInt(data, 27, 1) == 1;
        Output5 = ExtractSignalInt(data, 28, 1) == 1;
        Output6 = ExtractSignalInt(data, 29, 1) == 1;
        Output7 = ExtractSignalInt(data, 30, 1) == 1;
        Output8 = ExtractSignalInt(data, 31, 1) == 1;

        return true;
    }

    private byte[] Write()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.StarterDisable, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertSignalInt(data, (long)Input, 16, 8);
        InsertBool(data, Output1, 24);
        InsertBool(data, Output2, 25);
        InsertBool(data, Output3, 26);
        InsertBool(data, Output4, 27);
        InsertBool(data, Output5, 28);
        InsertBool(data, Output6, 29);
        InsertBool(data, Output7, 30);
        InsertBool(data, Output8, 31);
        return data;
    }
}