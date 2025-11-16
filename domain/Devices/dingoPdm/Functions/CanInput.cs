using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class CanInput(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("number")] public int Number {get; set;} = num;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("timeoutEnabled")] public bool TimeoutEnabled {get; set;}
    [JsonPropertyName("timeout")] public int Timeout {get; set;}
    [JsonPropertyName("ide")] public bool Ide {get; set;}
    [JsonPropertyName("startingByte")] public int StartingByte {get; set;}
    [JsonPropertyName("dlc")] public int Dlc {get; set;}
    [JsonPropertyName("operator")] public Operator Operator {get; set;}
    [JsonPropertyName("onVal")] public int OnVal {get; set;}
    [JsonPropertyName("mode")] public InputMode Mode {get; set;}

    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set
        {
            field = value;
            Ide = (field > 2047);
        }
    }
    
    [JsonIgnore] public bool Output { get; set; }
    [JsonIgnore] public int Value {get; set;}
    
    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.CanInputs);
        data[1] = Convert.ToByte(index);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 7) return false;

        Enabled = Convert.ToBoolean(data[2] & 0x01);
        Mode = (InputMode)((data[2] & 0x06) >> 1);
        TimeoutEnabled = Convert.ToBoolean((data[2] & 0x08) >> 3);
        Operator = (Operator)((data[2] & 0xF0) >> 4);
        Dlc = (data[3] & 0xF0) >> 4;
        StartingByte = (data[3] & 0x0F);
        OnVal = (data[4] << 8) + data[5];
        Timeout = (int)(data[6] / 10.0);
        return true;
    }

    public byte[] Write()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.CanInputs);
        data[1] = Convert.ToByte(Number - 1);
        data[2] = Convert.ToByte(((Convert.ToByte(Operator) & 0x0F) << 4) +
                                 ((Convert.ToByte(Mode) & 0x03) << 1) +
                                 Convert.ToByte((Convert.ToByte(TimeoutEnabled) << 3)) +
                                 (Convert.ToByte(Enabled) & 0x01));
        data[3] = Convert.ToByte(((Dlc & 0x0F) << 4) +
                                 (Convert.ToByte(StartingByte) & 0x0F));
        data[4] = Convert.ToByte((OnVal & 0xFF00) >> 8); 
        data[5] = Convert.ToByte(OnVal & 0x00FF);
        data[6] = Convert.ToByte((Timeout * 10));
        return data;
    }
    
    public static byte[] RequestId(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.CanInputsId);
        data[1] = Convert.ToByte(index);
        return data;
    }

    public bool ReceiveId(byte[] data)
    {
        if (data.Length != 8) return false;

        Ide = Convert.ToBoolean((data[2] & 0x08) >> 3);

        if (Ide)
        {
            Id = ((data[4] & 0x1F) << 24) + (data[5] << 16) + (data[6] << 8) + data[7];
        }
        else
        {
            Id = ((data[2] & 0x07) << 8) + data[3];
        }

        return true;
    }

    public byte[] WriteId()
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.CanInputsId);
        data[1] = Convert.ToByte(Number - 1);
        data[2] = Convert.ToByte((Convert.ToByte(Ide) << 3) +
                                 ((Id >> 8) & 0x07));
        data[3] = Convert.ToByte(Id & 0xFF);
        data[4] = Convert.ToByte((Id >> 24) & 0x1F);
        data[5] = Convert.ToByte((Id >> 16) & 0xFF);
        data[6] = Convert.ToByte((Id >> 8) & 0xFF);
        data[7] = Convert.ToByte(Id & 0xFF);
        return data;
    }
}