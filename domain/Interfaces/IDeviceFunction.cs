using domain.Models;

namespace domain.Interfaces;

public interface IDeviceFunction
{
    public int Number { get; }
    public string Name { get; }

    public List<DeviceParameter> Params { get; }
}
