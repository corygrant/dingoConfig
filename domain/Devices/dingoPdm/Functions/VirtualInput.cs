using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

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
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return data;
    }
    
    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.VirtualInputs) return null;
        
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.VirtualInputs, 0, 8);
        InsertSignalInt(data, Number - 1, 8, 8);

        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.VirtualInputs,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 2,
                Payload = data
            },
            MsgDescription = $"VirtualInput{Number}"
        };
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.VirtualInputs) return null;
        
        return new DeviceCanFrame
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.VirtualInputs,
            Index = Number - 1,
            Frame = new CanFrame
            {
                Id = baseId - 1,
                Len = 7,
                Payload = Write()
            },
            MsgDescription = $"VirtualInput{Number}"
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        if (prefix != MessagePrefix.VirtualInputs) return false;
        if (data.Length != 7) return false;

        Enabled = ExtractSignalInt(data, 8, 1) == 1;
        Not0 = ExtractSignalInt(data, 9, 1) == 1;   
        Not1 = ExtractSignalInt(data, 10, 1) == 1;  
        Not2 = ExtractSignalInt(data, 11, 1) == 1;  

        Var0 = (VarMap)ExtractSignalInt(data, 24, 8);
        Var1 = (VarMap)ExtractSignalInt(data, 32, 8);
        Var2 = (VarMap)ExtractSignalInt(data, 40, 8);

        Mode = (InputMode)ExtractSignalInt(data, 54, 2);
        Cond0 = (Conditional)ExtractSignalInt(data, 48, 2);
        Cond1 = (Conditional)ExtractSignalInt(data, 50, 2);

        return true;
    }

    private byte[] Write()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.VirtualInputs, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertBool(data, Not0, 9);
        InsertBool(data, Not1, 10);
        InsertBool(data, Not2, 11);
        InsertSignalInt(data, Number - 1, 16, 8);
        InsertSignalInt(data, (long)Var0, 24, 8);
        InsertSignalInt(data, (long)Var1, 32, 8);
        InsertSignalInt(data, (long)Var2, 40, 8);
        InsertSignalInt(data, (long)Mode, 54, 2);
        InsertSignalInt(data, (long)Cond1, 50, 2);
        InsertSignalInt(data, (long)Cond0, 48, 2);
        return data;
    }
}