using domain.Models;

namespace domain.Interfaces;

public interface IDeviceConfigurable : IDevice
{
    List<DeviceVariable> VarMap { get; }
    List<DeviceParameter> Params { get; }
    bool ConfigMismatch { get; set; }
    DeviceCanFrame GetCheckMsg();
    List<DeviceCanFrame> GetReadMsgs(bool allParams);
    List<DeviceCanFrame> GetWriteMsgs(bool allParams);
    List<DeviceCanFrame> GetModifyMsgs(DeviceIds ids);
    DeviceCanFrame GetBurnMsg();
    DeviceCanFrame GetVersionMsg();
    bool CanSleep { get; }
    DeviceCanFrame? GetSleepMsg();
    DeviceCanFrame? GetWakeupMsg();
    bool CanBootloader { get; }
    DeviceCanFrame? GetBootloaderMsg();
    
}