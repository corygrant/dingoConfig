namespace domain.Enums;

public enum MessageSrc
{
    StateRun = 1,
    StateSleep,
    StateOvertemp,
    StateError,
    OverCurrent,
    BatteryVoltage,
    CAN,
    USB,
    OverTemp,
    Config,
    FRAM,
    ADC,
    I2C,
    TempSensor,
    USBConnected,
    Init
}