using domain.Devices;
using domain.Models;

namespace domain.Interfaces;

public interface IDeviceConfigurable : IDevice
{
    FwDeviceDef Def { get; }
    List<DeviceVariable> VarMap { get; }
    List<DeviceParameter> Params { get; }
    bool ConfigMismatch { get; set; }
    DeviceCanFrame GetCheckMsg();
    List<DeviceCanFrame> GetReadMsgs(bool allParams);
    List<DeviceCanFrame> GetWriteMsgs(bool allParams);
    DeviceCanFrame GetModifyMsg(int baseId);
    DeviceCanFrame GetBurnMsg();
    DeviceCanFrame GetVersionMsg();
    DeviceCanFrame? GetSleepMsg();
    DeviceCanFrame? GetWakeupMsg();
    DeviceCanFrame? GetBootloaderMsg();
    
}