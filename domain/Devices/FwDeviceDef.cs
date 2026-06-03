namespace domain.Devices;

public record FwDeviceDef(
    int DeviceType,
    string DefaultId, //string to store json id as hex
    string TypeName,
    string Icon,
    string SignalsFile,
    int NumDigitalInputs,
    int NumDigitalOutputs,
    int NumAnalogInputs,
    int NumOutputs,
    int NumCanInputs,
    int NumCanOutputs,
    int NumVirtualInputs,
    int NumFlashers,
    int NumCounters,
    int NumConditions,
    int NumKeypads,
    bool HasWipers,
    bool HasStarterDisable,
    bool HasBattVoltSense,
    bool HasUsb,
    bool HasExtTempSensor,
    bool CanSleep,
    bool CanBootloader,
    int MinMajorVersion,
    int MinMinorVersion,
    int MinBuildVersion
    );
