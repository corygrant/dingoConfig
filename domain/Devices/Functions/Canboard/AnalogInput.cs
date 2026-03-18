using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions.Canboard;

public class AnalogInput(int number, string name) : IDeviceFunction
{
    [JsonPropertyName("number")] public int Number { get; set; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    
    [JsonIgnore][Plotable(displayName:"Millivolts")] public double Millivolts { get; set; }
    [JsonIgnore][Plotable(displayName:"RotarySwPos")] public int RotarySwitchPos { get; set; }
    [JsonIgnore][Plotable(displayName:"DigIn")] public bool DigitalIn { get; set; }
    
    [JsonIgnore] public List<DeviceParameter> Params { get; } = [];
    
    public static int ExtractIndex(byte data, MessageCommand command)
    {
        throw new NotImplementedException();
    }

    public bool Receive(byte[] data, MessageCommand command)
    {
        throw new NotImplementedException();
    }

    public DeviceCanFrame? CreateUploadRequest(int baseId, MessageCommand command)
    {
        throw new NotImplementedException();
    }

    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessageCommand command)
    {
        throw new NotImplementedException();
    }
}