using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm.Functions;

public class Output(int num, string name) : IDeviceFunction
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("number")] public int Number { get; set; } = num;
    [JsonPropertyName("currentLimit")] public int CurrentLimit { get; set; }
    [JsonPropertyName("resetCountLimit")] public int ResetCountLimit { get; set; }
    [JsonPropertyName("resetMode")] public ResetMode ResetMode { get; set; }
    [JsonPropertyName("resetTime")] public double ResetTime { get; set; }
    [JsonPropertyName("inrushCurrentLimit")] public int InrushCurrentLimit { get; set; }
    [JsonPropertyName("inrushTime")] public double InrushTime { get; set; }
    [JsonPropertyName("input")] public VarMap Input { get; set; }
    [JsonPropertyName("softStartEnabled")] public bool SoftStartEnabled { get; set; }
    [JsonPropertyName("variableDutyCycle")] public bool VariableDutyCycle { get; set; }
    [JsonPropertyName("dutyCycleInput")] public VarMap DutyCycleInput { get; set; }
    [JsonPropertyName("fixedDutyCycle")] public int FixedDutyCycle { get; set; }
    [JsonPropertyName("frequency")] public int Frequency { get; set; }
    [JsonPropertyName("softStartRampTime")] public int SoftStartRampTime { get; set; }
    [JsonPropertyName("dutyCycleDenominator")] public int DutyCycleDenominator { get; set; }
    
    [JsonIgnore] public double Current { get; set; }
    [JsonIgnore] public OutState State { get; set; }
    [JsonIgnore] public int ResetCount { get; set; }
    [JsonIgnore] public double CurrentDutyCycle { get; set; }
    [JsonIgnore] public double CalculatedPower { get; set; }
    
    public static int ExtractIndex(byte data, MessagePrefix prefix)
    {
        return (data & 0xF0) >> 4;
    }
    
    public DeviceResponse? CreateUploadRequest(int baseId, MessagePrefix prefix)
    {
        switch (prefix)
        {
            case MessagePrefix.Outputs:
            {
                var data = new byte[8];
                InsertSignalInt(data, (long)MessagePrefix.Outputs, 0, 8);
                InsertSignalInt(data, Number - 1, 12, 4);

                return new DeviceResponse
                {
                    Sent = false,
                    Received = false,
                    Prefix = (int)MessagePrefix.Outputs,
                    Index = Number - 1,
                    Data = new CanData
                    {
                        Id = baseId - 1,
                        Len = 2,
                        Payload = data
                    },
                    MsgDescription = $"Output{Number}"
                };
            }
            case MessagePrefix.OutputsPwm:
            {
                var data = new byte[8];
                InsertSignalInt(data, (long)MessagePrefix.OutputsPwm, 0, 8);
                InsertSignalInt(data, Number - 1, 12, 4);

                return new DeviceResponse
                {
                    Sent = false,
                    Received = false,
                    Prefix = (int)MessagePrefix.OutputsPwm,
                    Index = Number - 1,
                    Data = new CanData
                    {
                        Id = baseId - 1,
                        Len = 2,
                        Payload = data
                    },
                    MsgDescription = $"OutputPwm{Number}"
                };
            }
            default:
                return null;
        }
    }

    public DeviceResponse? CreateDownloadRequest(int baseId, MessagePrefix prefix)
    {
        return prefix switch
        {
            MessagePrefix.Outputs => new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Outputs,
                Index = Number - 1,
                Data = new CanData { Id = baseId - 1, Len = 8, Payload = Write() },
                MsgDescription = $"Output{Number}"
            },
            MessagePrefix.OutputsPwm => new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.OutputsPwm,
                Index = Number - 1,
                Data = new CanData { Id = baseId - 1, Len = 8, Payload = WritePwm() },
                MsgDescription = $"OutputPwm{Number}"
            },
            _ => null
        };
    }

    public bool Receive(byte[] data, MessagePrefix prefix)
    {
        switch (prefix)
        {
            case MessagePrefix.Outputs when data.Length != 8:
                return false;
            case MessagePrefix.Outputs:
                Enabled = ExtractSignalInt(data, 8, 1) == 1;
                Input = (VarMap)ExtractSignalInt(data, 16, 8);
                CurrentLimit = (int)ExtractSignalInt(data, 24, 8);
                ResetMode = (ResetMode)ExtractSignalInt(data, 32, 4);
                ResetCountLimit = (int)ExtractSignalInt(data, 36, 4);
                ResetTime = ExtractSignal(data, 40, 8, factor: 0.1);
                InrushCurrentLimit = (int)ExtractSignalInt(data, 48, 8);
                InrushTime = ExtractSignal(data, 56, 8, factor: 0.1);

                return true;
            case MessagePrefix.OutputsPwm when data.Length != 8:
                return false;
            case MessagePrefix.OutputsPwm:
            {
                Enabled = ExtractSignalInt(data, 8, 1) == 1;
                SoftStartEnabled = ExtractSignalInt(data, 9, 1) == 1;
                VariableDutyCycle = ExtractSignalInt(data, 10, 1) == 1;
                DutyCycleInput = (VarMap)ExtractSignalInt(data, 16, 8);
    
                // Frequency is 9 bits with unusual encoding: upper 8 bits at byte 3, LSB at byte 4 bit 0
                int freqUpper = (int)ExtractSignalInt(data, 24, 8);
                int freqLsb = (int)ExtractSignalInt(data, 32, 1);
                Frequency = (freqUpper << 1) | freqLsb;
    
                FixedDutyCycle = (byte)ExtractSignalInt(data, 33, 7);
                SoftStartRampTime = (ushort)ExtractSignalInt(data, 40, 16, ByteOrder.BigEndian);
                DutyCycleDenominator = (byte)ExtractSignalInt(data, 56, 8);

                return true;
            }
            default:
                return false;
        }
    }

    private byte[] Write()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.Outputs, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertSignalInt(data, Number - 1, 12, 4);
        InsertSignalInt(data, (long)Input, 16, 8);
        InsertSignalInt(data, CurrentLimit, 24, 8);
        InsertSignalInt(data, (long)ResetMode, 32, 4);
        InsertSignalInt(data, ResetCountLimit, 36, 4);
        InsertSignal(data, ResetTime, 40, 8, factor: 0.1);
        InsertSignalInt(data, InrushCurrentLimit, 48, 8);
        InsertSignal(data, InrushTime, 56, 8, factor: 0.1);
        return data;
    }

    private byte[] WritePwm()
    {
        var data = new byte[8];
        InsertSignalInt(data, (long)MessagePrefix.OutputsPwm, 0, 8);
        InsertBool(data, Enabled, 8);
        InsertBool(data, SoftStartEnabled, 9);
        InsertBool(data, VariableDutyCycle, 10);
        InsertSignalInt(data, Number - 1, 12, 4);
        InsertSignalInt(data, (long)DutyCycleInput, 16, 8);

        // Frequency is 9 bits with unusual encoding: upper 8 bits at byte 3, LSB at byte 4 bit 0
        InsertSignalInt(data, Frequency >> 1, 24, 8);
        InsertSignalInt(data, Frequency & 0x01, 32, 1);

        InsertSignalInt(data, FixedDutyCycle, 33, 7);
        InsertSignalInt(data, SoftStartRampTime, 40, 16, ByteOrder.BigEndian);
        InsertSignalInt(data, DutyCycleDenominator, 56, 8);

        return data;
    }
}