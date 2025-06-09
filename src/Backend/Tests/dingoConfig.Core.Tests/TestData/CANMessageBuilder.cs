using dingoConfig.Core.Models;

namespace dingoConfig.Core.Tests.TestData;

public class CANMessageBuilder
{
    private uint _id = TestConstants.CanIds.MainStatus;
    private byte[] _data = new byte[8];
    private DateTime _timestamp = DateTime.UtcNow;
    private bool _isExtended = false;
    private bool _isRemote = false;

    public CANMessageBuilder WithId(uint id)
    {
        _id = id;
        return this;
    }

    public CANMessageBuilder WithData(params byte[] data)
    {
        _data = new byte[8];
        Array.Copy(data, _data, Math.Min(data.Length, 8));
        return this;
    }

    public CANMessageBuilder WithTimestamp(DateTime timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    public CANMessageBuilder WithExtendedId(bool isExtended = true)
    {
        _isExtended = isExtended;
        return this;
    }

    public CANMessageBuilder WithRemoteFrame(bool isRemote = true)
    {
        _isRemote = isRemote;
        return this;
    }

    public CANMessageBuilder WithVoltageData(float voltage)
    {
        var voltageBytes = BitConverter.GetBytes((ushort)(voltage * 100));
        return WithData(voltageBytes[0], voltageBytes[1], 0, 0, 0, 0, 0, 0);
    }

    public CANMessageBuilder WithCurrentData(float current)
    {
        var currentBytes = BitConverter.GetBytes((ushort)(current * 100));
        return WithData(0, 0, currentBytes[0], currentBytes[1], 0, 0, 0, 0);
    }

    public CANMessageBuilder WithTemperatureData(float temperature)
    {
        var tempBytes = BitConverter.GetBytes((ushort)((temperature + 40) * 10));
        return WithId(TestConstants.CanIds.Temperature)
               .WithData(tempBytes[0], tempBytes[1], 0, 0, 0, 0, 0, 0);
    }

    public CANMessage Build()
    {
        return new CANMessage
        {
            Id = _id,
            Data = _data,
            Timestamp = _timestamp,
            IsExtended = _isExtended,
            IsRemote = _isRemote
        };
    }

    public static CANMessage CreateMainStatus(float voltage = 12.5f, float current = 8.2f)
    {
        return new CANMessageBuilder()
            .WithId(TestConstants.CanIds.MainStatus)
            .WithVoltageData(voltage)
            .WithCurrentData(current)
            .Build();
    }

    public static CANMessage CreateTemperature(float temperature = 25.0f)
    {
        return new CANMessageBuilder()
            .WithTemperatureData(temperature)
            .Build();
    }

    public static CANMessage CreateOutputStatus(int outputChannel, float current)
    {
        var currentBytes = BitConverter.GetBytes((ushort)(current * 100));
        return new CANMessageBuilder()
            .WithId(TestConstants.CanIds.OutputStatus)
            .WithData((byte)outputChannel, 0, currentBytes[0], currentBytes[1], 0, 0, 0, 0)
            .Build();
    }
}