using domain.Models;

namespace domain.Interfaces;

public interface IDeviceConfigurable : IDevice
{
    int ParamTxId {get; set; }
    int ParamRxId {get; set; }
    List<DeviceVariable> VarMap { get; }
    List<DeviceParameter> Params { get; }
    bool ConfigMismatch { get; set; }
    DeviceCanFrame GetCheckMsg();
    List<DeviceCanFrame> GetReadMsgs(bool allParams);
    List<DeviceCanFrame> GetWriteMsgs(bool allParams);
    List<DeviceCanFrame> GetModifyMsgs(int newId);
    DeviceCanFrame GetBurnMsg();
    DeviceCanFrame GetVersionMsg();
    bool CanSleep { get; }
    DeviceCanFrame? GetSleepMsg();
    DeviceCanFrame? GetWakeupMsg();
    bool CanBootloader { get; }
    DeviceCanFrame? GetBootloaderMsg();
    
}