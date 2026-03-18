using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions.Canboard;

public class DigitalOutput(int number, string name) : IDeviceFunction
{
    [JsonPropertyName("number")] public int Number { get; set; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonIgnore] public List<DeviceParameter> Parameters { get; } = [];

    [JsonIgnore][Plotable(displayName:"State")] public bool State { get; set; }
    
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