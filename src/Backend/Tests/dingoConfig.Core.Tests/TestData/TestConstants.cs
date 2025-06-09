namespace dingoConfig.Core.Tests.TestData;

public static class TestConstants
{
    public const string ValidDeviceId = "dingoPDM_001";
    public const string ValidDeviceType = "dingoPDM";
    public const string ValidDeviceName = "Test Power Distribution Module";
    public const int ValidCanNodeId = 1;
    public const int ValidBaudRate = 500000;
    public const uint ValidCanBaseId = 0x600;
    
    public const string SampleCatalogPath = "../../../../test-data/sample-dingoPDM-catalog.json";
    public const string SampleConfigPath = "../../../../test-data/sample-config-dingoPDM.json";
    
    public static class ParameterNames
    {
        public const string Output1Enabled = "output1_enabled";
        public const string Output1CurrentLimit = "output1_current_limit";
        public const string CanNodeId = "can_node_id";
        public const string DiagnosticInterval = "diagnostic_interval";
    }
    
    public static class TelemetryNames
    {
        public const string InputVoltage = "input_voltage";
        public const string TotalCurrent = "total_current";
        public const string DeviceTemperature = "device_temperature";
    }
    
    public static class CanIds
    {
        public const uint MainStatus = 0x600;
        public const uint OutputStatus = 0x601;
        public const uint Temperature = 0x602;
        public const uint Commands = 0x650;
    }
}