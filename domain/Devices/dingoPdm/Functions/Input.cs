using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;

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

    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Inputs);
        data[1] = Convert.ToByte((index & 0x0F) << 4);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 4) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Invert = Convert.ToBoolean((data[1] & 0x08) >> 3);
        Mode = (InputMode)((data[1] & 0x06) >> 1);
        DebounceTime = data[2] * 10;
        Pull = (InputPull)(data[3] & 0x03);

        return true;
    }

    public byte[] Write()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Inputs);
        data[1] = Convert.ToByte((((Number - 1) & 0x0F) << 4) +
                                 ((Convert.ToByte(Invert) & 0x01) << 3) +
                                 ((Convert.ToByte(Mode) & 0x03) << 1) +
                                 (Convert.ToByte(Enabled) & 0x01));
        data[2] = Convert.ToByte(DebounceTime / 10);
        data[3] = Convert.ToByte((byte)Pull & 0x03);
        return data;
    }
}