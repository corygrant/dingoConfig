namespace domain.Devices.Canboard;

public record CanboardDeviceDefinition(
    int CanboardType,
    string TypeName,
    string Icon,
    int NumAnalogInputs,
    int NumDigitalInputs,
    int NumOutputs,
    int NumCanInputs,
    int NumCanOutputs,
    int NumVirtualInputs,
    int NumFlashers,
    int NumCounters,
    int NumConditions,
    int MinMajorVersion,
    int MinMinorVersion,
    int MinBuildVersion
);