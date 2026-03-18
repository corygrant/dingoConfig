namespace domain.Devices.dingoPdm;

public record PdmDeviceDefinition(
    int PdmType,
    string TypeName,
    string Icon,
    int NumDigitalInputs,
    int NumOutputs,
    int NumCanInputs,
    int NumCanOutputs,
    int NumVirtualInputs,
    int NumFlashers,
    int NumCounters,
    int NumConditions,
    int NumKeypads,
    int MinMajorVersion,
    int MinMinorVersion,
    int MinBuildVersion
    );
