using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Interfaces;

namespace domain.Devices.dingoPdm.Functions;

public class Wiper(string name) : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name {get; set;} = name;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("mode")] public WiperMode Mode { get; set; }
    [JsonPropertyName("slowInput")] public VarMap SlowInput { get; set; }
    [JsonPropertyName("fastInput")] public VarMap FastInput { get; set; }
    [JsonPropertyName("interInput")] public VarMap InterInput { get; set; }
    [JsonPropertyName("onInput")] public VarMap OnInput { get; set; }
    [JsonPropertyName("speedInput")] public VarMap SpeedInput { get; set; }
    [JsonPropertyName("parkInput")] public VarMap ParkInput { get; set; }
    [JsonPropertyName("parkStopLevel")] public bool ParkStopLevel { get; set; }
    [JsonPropertyName("swipeInput")] public VarMap SwipeInput { get; set; }
    [JsonPropertyName("washInput")] public VarMap WashInput { get; set; }
    [JsonPropertyName("washWipeCycles")] public int WashWipeCycles { get; set; }
    [JsonPropertyName("speedMap")] public WiperSpeed[] SpeedMap { get; set; } = new WiperSpeed[8];
    [JsonPropertyName("intermitTime")] public double[] IntermitTime { get; set; } = new double[6];

    [JsonIgnore] public bool SlowState { get; set; }
    [JsonIgnore] public bool FastState { get; set; }
    [JsonIgnore] public WiperState State { get; set; }
    [JsonIgnore] public WiperSpeed Speed { get; set; }

    public static byte[] Request(int index)
    {
        var data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Wiper);
        return data;
    }

    public bool Receive(byte[] data)
    {
        if (data.Length != 8) return false;

        Enabled = Convert.ToBoolean(data[1] & 0x01);
        Mode = (WiperMode)((data[1] & 0x06) >> 1);
        ParkStopLevel = Convert.ToBoolean((data[1] & 0x08) >> 3);
        WashWipeCycles = (data[1] & 0xF0) >> 4;
        SlowInput = (VarMap)data[2];
        FastInput = (VarMap)data[3];
        InterInput = (VarMap)data[4];
        OnInput = (VarMap)data[5];
        ParkInput = (VarMap)data[6];
        WashInput = (VarMap)data[7];

        return true;
    }

    public byte[] Write()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.Wiper);
        data[1] = Convert.ToByte(((Convert.ToByte(WashWipeCycles) & 0x0F) << 4) +
                                 ((Convert.ToByte(ParkStopLevel) & 0x01) << 3) +
                                 ((Convert.ToByte(Mode) & 0x03) << 1) +
                                 (Convert.ToByte(Enabled) & 0x01));
        data[2] = Convert.ToByte(SlowInput);
        data[3] = Convert.ToByte(FastInput);
        data[4] = Convert.ToByte(InterInput);
        data[5] = Convert.ToByte(OnInput);
        data[6] = Convert.ToByte(ParkInput);
        data[7] = Convert.ToByte(WashInput);
        return data;
    }
    
    public static byte[] RequestSpeed()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.WiperSpeed);
        return data;
    }

    public bool ReceiveSpeed(byte[] data)
    {
        if (data.Length != 7) return false;

        SwipeInput = (VarMap)data[1];
        SpeedInput = (VarMap)data[2];
        SpeedMap[0] = (WiperSpeed)(data[3] & 0x0F);
        SpeedMap[1] = (WiperSpeed)((data[3] & 0xF0) >> 4);
        SpeedMap[2] = (WiperSpeed)(data[4] & 0x0F);
        SpeedMap[3] = (WiperSpeed)((data[4] & 0xF0) >> 4);
        SpeedMap[4] = (WiperSpeed)(data[5] & 0x0F);
        SpeedMap[5] = (WiperSpeed)((data[5] & 0xF0) >> 4);
        SpeedMap[6] = (WiperSpeed)(data[6] & 0x0F);
        SpeedMap[7] = (WiperSpeed)((data[6] & 0xF0) >> 4);

        return true;
    }

    public byte[] WriteSpeed()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.WiperSpeed);
        data[1] = Convert.ToByte(SwipeInput);
        data[2] = Convert.ToByte(SpeedInput);
        data[3] = Convert.ToByte(((Convert.ToByte(SpeedMap[1]) & 0x0F) << 4) +
                  (Convert.ToByte(SpeedMap[0]) & 0x0F));
        data[4] = Convert.ToByte(((Convert.ToByte(SpeedMap[3]) & 0x0F) << 4) +
                  (Convert.ToByte(SpeedMap[2]) & 0x0F));
        data[5] = Convert.ToByte(((Convert.ToByte(SpeedMap[4]) & 0x0F) << 4) +
                  (Convert.ToByte(SpeedMap[5]) & 0x0F));
        data[6] = Convert.ToByte(((Convert.ToByte(SpeedMap[7]) & 0x0F) << 4) +
                  (Convert.ToByte(SpeedMap[6]) & 0x0F));
                
        return data;
    }

    public static byte[] RequestDelays()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.WiperDelays);
        return data;
    }

    public bool ReceiveDelays(byte[] data)
    {
        if (data.Length != 7) return false;

        IntermitTime[0] = data[1] / 10.0;
        IntermitTime[1] = data[2] / 10.0;
        IntermitTime[2] = data[3] / 10.0;
        IntermitTime[3] = data[4] / 10.0;
        IntermitTime[4] = data[5] / 10.0;
        IntermitTime[5] = data[6] / 10.0;

        return true;
    }

    public byte[] WriteDelays()
    {
        byte[] data = new byte[8];
        data[0] = Convert.ToByte(MessagePrefix.WiperDelays);
        data[1] = Convert.ToByte(IntermitTime[0] * 10);
        data[2] = Convert.ToByte(IntermitTime[1] * 10);
        data[3] = Convert.ToByte(IntermitTime[2] * 10);
        data[4] = Convert.ToByte(IntermitTime[3] * 10);
        data[5] = Convert.ToByte(IntermitTime[4] * 10);
        data[6] = Convert.ToByte(IntermitTime[5] * 10);
               
        return data;
    }
}