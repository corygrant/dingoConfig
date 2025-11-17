using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Enums;
using domain.Devices.dingoPdm.Functions;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using static domain.Common.DbcSignalCodec;

namespace domain.Devices.dingoPdm;

public class dingoPdmDevice : IDevice
{
    protected virtual int MinMajorVersion { get; } = 0;
    protected virtual int MinMinorVersion { get; } = 4;
    protected virtual int MinBuildVersion { get; } = 18;

    protected virtual int NumDigitalInputs { get; } = 2;
    protected virtual int NumOutputs { get; } = 8;
    protected virtual int NumCanInputs { get; } = 32;
    protected virtual int NumVirtualInputs { get; } = 16;
    protected virtual int NumFlashers { get; } = 4;
    protected virtual int NumCounters { get; } = 4;
    protected virtual int NumConditions { get; } = 32;
    
    protected int PdmType;
    
    [JsonIgnore] public Guid Guid { get; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }

    [JsonIgnore] public DateTime LastRxTime { get; set; }
    [JsonIgnore] public DeviceState DeviceState { get; set; }
    [JsonIgnore] public double TotalCurrent { get; set; }
    [JsonIgnore] public double BatteryVoltage { get; set; }
    [JsonIgnore] public double BoardTempC { get; set; }
    [JsonIgnore] public double BoardTempF { get; set; }
    [JsonIgnore] public string Version { get; set; } = "";
    [JsonIgnore] public bool SleepEnabled { get; set; }
    [JsonIgnore] public bool CanFiltersEnabled { get; set; }
    [JsonIgnore] public CanBitRate BitRate { get; set; }
    
    [JsonPropertyName("digitalInputs")] protected List<Input> Inputs { get; set; } = new List<Input>();
    [JsonPropertyName("outputs")] protected List<Output> Outputs { get; set; } = new List<Output>();
    [JsonPropertyName("canInputs")] protected List<CanInput> CanInputs { get; set; } = new List<CanInput>();
    [JsonPropertyName("virtualInputs")] protected List<VirtualInput> VirtualInputs { get; set; } = new List<VirtualInput>();
    [JsonPropertyName("wipers")] protected Wiper Wipers { get; set; } = new Wiper("wiper");
    [JsonPropertyName("flashers")] protected List<Flasher> Flashers { get; set; } = new List<Flasher>();
    [JsonPropertyName("starterDisable")] protected StarterDisable StarterDisable { get; set; } = new StarterDisable("starterDisable");
    [JsonPropertyName("counters")] protected  List<Counter> Counters { get; set; } = new List<Counter>();
    [JsonPropertyName("conditions")] protected List<Condition> Conditions { get; set; } = new List<Condition>();
    
    public bool Connected
    {
        get;
        set
        {
            if (field != value)
            {
                Clear();
                
                field = value;
            }
        }
    }
    
    public dingoPdmDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;
        Guid = Guid.NewGuid();

        for (var i = 0; i < NumDigitalInputs; i++)
            Inputs.Add(new Input(i + 1, "digitalInput" + i));
        
        for (var i = 0; i < NumOutputs; i++)
            Outputs.Add(new Output(i + 1, "output" + i));

        for (var i = 0; i < NumCanInputs; i++)
            CanInputs.Add(new CanInput(i + 1, "canInput" + i));

        for (var i = 0; i < NumVirtualInputs; i++)
            VirtualInputs.Add(new VirtualInput(i + 1, "virtualInput" + i));

        for (var i = 0; i < NumFlashers; i++)
            Flashers.Add(new Flasher(i + 1,  "flasher" + i));

        for (var i = 0; i < NumCounters; i++)
            Counters.Add(new Counter(i  + 1, "counter" + i));

        for (var i = 0; i < NumConditions; i++)
            Conditions.Add(new Condition(i + 1, "condition" + i));
    }
    public void UpdateConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }

    public void Clear()
    {
        foreach(var input in Inputs)
            input.State = false;

        foreach(var output in Outputs)
        {
            output.Current = 0;
            output.State = OutState.Off;
        }

        foreach(var input in VirtualInputs)
            input.Value = false;

        foreach(var canInput in CanInputs)
            canInput.Output = false;
    }

    private void UpdateIsConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }
    
    public bool InIdRange(int id)
    {
        return (id >= BaseId) && (id <= BaseId + 31);
    }
    
    public bool Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceResponse> queue)
    {
        if (!InIdRange(id)) 
            return false;

        int offset = id - BaseId;
        switch (offset)
        {
            case 0: ReadMessage0(data); break;
            case 1: ReadMessage1(data); break;
            case 2: ReadMessage2(data); break;
            case 3: ReadMessage3(data); break;
            case 4: ReadMessage4(data); break;
            case 5: ReadMessage5(data); break;
            case 6: ReadMessage6(data); break;
            case 7: ReadMessage7(data); break;
            case 8: ReadMessage8(data); break;
            case 9: ReadMessage9(data); break;
            case 10: ReadMessage10(data); break;
            case 11: ReadMessage11(data); break;
            case 12: ReadMessage12(data); break;
            case 13: ReadMessage13(data); break;
            case 14: ReadMessage14(data); break;
            case 15: ReadMessage15(data); break;
            case 30: ReadSettingsResponse(data, queue); break;
            case 31: ReadInfoWarnErrorMessage(data); break;
        }

        LastRxTime = DateTime.Now;
        UpdateIsConnected();
        return true;
    }

    protected void ReadMessage0(byte[] data)
    {
        Inputs[0].State = ExtractSignalInt(data, 0, 1) == 1;
        Inputs[1].State = ExtractSignalInt(data, 1, 1) == 1;

        DeviceState = (DeviceState)ExtractSignalInt(data, 8, 4);
        PdmType = (int)ExtractSignalInt(data, 12, 4);

        TotalCurrent = ExtractSignal(data, 16, 16, factor: 0.1);
        BatteryVoltage = ExtractSignal(data, 32, 16, factor: 0.1);
        BoardTempC = Math.Round(ExtractSignal(data, 48, 16, factor: 0.1), 1);
        BoardTempF = Math.Round(BoardTempC * 1.8 + 32);
    }

    protected void ReadMessage1(byte[] data)
    {
        Outputs[0].Current = ExtractSignal(data, 0, 16, factor: 0.1);
        Outputs[1].Current = ExtractSignal(data, 16, 16, factor: 0.1);
        Outputs[2].Current = ExtractSignal(data, 32, 16, factor: 0.1);
        Outputs[3].Current = ExtractSignal(data, 48, 16, factor: 0.1);
    }

    protected virtual void ReadMessage2(byte[] data)
    {
        Outputs[4].Current = ExtractSignal(data, 0, 16, factor: 0.1);
        Outputs[5].Current = ExtractSignal(data, 16, 16, factor: 0.1);
        Outputs[6].Current = ExtractSignal(data, 32, 16, factor: 0.1);
        Outputs[7].Current = ExtractSignal(data, 48, 16, factor: 0.1);
    }

    protected virtual void ReadMessage3(byte[] data)
    {
        Outputs[0].State = (OutState)ExtractSignalInt(data, 0, 4);
        Outputs[1].State = (OutState)ExtractSignalInt(data, 4, 4);
        Outputs[2].State = (OutState)ExtractSignalInt(data, 8, 4);
        Outputs[3].State = (OutState)ExtractSignalInt(data, 12, 4);
        Outputs[4].State = (OutState)ExtractSignalInt(data, 16, 4);
        Outputs[5].State = (OutState)ExtractSignalInt(data, 20, 4);
        Outputs[6].State = (OutState)ExtractSignalInt(data, 24, 4);
        Outputs[7].State = (OutState)ExtractSignalInt(data, 28, 4);

        Wipers.SlowState = ExtractSignalInt(data, 32, 1) == 1;
        Wipers.FastState = ExtractSignalInt(data, 33, 1) == 1;
        Wipers.State = (WiperState)ExtractSignalInt(data, 44, 4);
        Wipers.Speed = (WiperSpeed)ExtractSignalInt(data, 40, 4);

        Flashers[0].Value = ExtractSignalInt(data, 48, 1) == 1 && Flashers[0].Enabled;
        Flashers[1].Value = ExtractSignalInt(data, 49, 1) == 1 && Flashers[1].Enabled;
        Flashers[2].Value = ExtractSignalInt(data, 50, 1) == 1 && Flashers[2].Enabled;
        Flashers[3].Value = ExtractSignalInt(data, 51, 1) == 1 && Flashers[3].Enabled;
    }

    protected virtual void ReadMessage4(byte[] data)
    {
        Outputs[0].ResetCount = (int)ExtractSignalInt(data, 0, 8);
        Outputs[1].ResetCount = (int)ExtractSignalInt(data, 8, 8);
        Outputs[2].ResetCount = (int)ExtractSignalInt(data, 16, 8);
        Outputs[3].ResetCount = (int)ExtractSignalInt(data, 24, 8);
        Outputs[4].ResetCount = (int)ExtractSignalInt(data, 32, 8);
        Outputs[5].ResetCount = (int)ExtractSignalInt(data, 40, 8);
        Outputs[6].ResetCount = (int)ExtractSignalInt(data, 48, 8);
        Outputs[7].ResetCount = (int)ExtractSignalInt(data, 56, 8);
    }

    protected virtual void ReadMessage5(byte[] data)
    {
        for (int i = 0; i < 32; i++)
        {
            CanInputs[i].Output = ExtractSignalInt(data, i, 1) == 1;
        }

        for (int i = 0; i < 16; i++)
        {
            VirtualInputs[i].Value = ExtractSignalInt(data, 32 + i, 1) == 1;
        }
    }

    protected virtual void ReadMessage6(byte[] data)
    {
        Counters[0].Value = (int)ExtractSignalInt(data, 0, 8);
        Counters[1].Value = (int)ExtractSignalInt(data, 8, 8);
        Counters[2].Value = (int)ExtractSignalInt(data, 16, 8);
        Counters[3].Value = (int)ExtractSignalInt(data, 24, 8);

        for (int i = 0; i < 32; i++)
        {
            Conditions[i].Value = (int)ExtractSignalInt(data, 32 + i, 1);
        }
    }

    protected virtual void ReadMessage7(byte[] data)
    {
        CanInputs[0].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[1].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[2].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[3].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage8(byte[] data)
    {
        CanInputs[4].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[5].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[6].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[7].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage9(byte[] data)
    {
        CanInputs[8].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[9].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[10].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[11].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage10(byte[] data)
    {
        CanInputs[12].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[13].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[14].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[15].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage11(byte[] data)
    {
        CanInputs[16].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[17].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[18].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[19].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage12(byte[] data)
    {
        CanInputs[20].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[21].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[22].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[23].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage13(byte[] data)
    {
        CanInputs[24].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[25].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[26].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[27].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage14(byte[] data)
    {
        CanInputs[28].Value = (ushort)ExtractSignalInt(data, 0, 16);
        CanInputs[29].Value = (ushort)ExtractSignalInt(data, 16, 16);
        CanInputs[30].Value = (ushort)ExtractSignalInt(data, 32, 16);
        CanInputs[31].Value = (ushort)ExtractSignalInt(data, 48, 16);
    }

    protected virtual void ReadMessage15(byte[] data)
    {
        Outputs[0].CurrentDutyCycle = ExtractSignal(data, 0, 8);
        Outputs[1].CurrentDutyCycle = ExtractSignal(data, 8, 8);
        Outputs[2].CurrentDutyCycle = ExtractSignal(data, 16, 8);
        Outputs[3].CurrentDutyCycle = ExtractSignal(data, 24, 8);
        Outputs[4].CurrentDutyCycle = ExtractSignal(data, 32, 8);
        Outputs[5].CurrentDutyCycle = ExtractSignal(data, 40, 8);
        Outputs[6].CurrentDutyCycle = ExtractSignal(data, 48, 8);
        Outputs[7].CurrentDutyCycle = ExtractSignal(data, 56, 8);
    }

    protected void ReadSettingsResponse(byte[] data, ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceResponse> queue)
    {
        //Response is prefix + 128
        if (data[0] < 128)
            return;
            
        var prefix = (MessagePrefix)(data[0] - 128);

        var index = 0;

        //Vars used below
        (int BaseId, int, int) key;
        DeviceResponse response;

        switch (prefix)
        {
            case MessagePrefix.Version:
                Version = $"V{data[1]}.{data[2]}.{(data[3] << 8) + (data[4])}";

                if (!CheckVersion(data[1], data[2], (data[3] << 8) + (data[4])))
                {
                    //TODO: Add log
                    //Logger.Error($"{Name} ID: {BaseId}, Firmware needs to be updated. V{_minMajorVersion}.{_minMinorVersion}.{_minBuildVersion} or greater");
                }

                key = (BaseId, (int)MessagePrefix.Version, 0);
                if (queue.TryGetValue(key, out response!))
                {
                    response.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                break;

            case MessagePrefix.Can:
                SleepEnabled = Convert.ToBoolean(data[1] & 0x01);
                CanFiltersEnabled = Convert.ToBoolean((data[1] >> 1) & 0x01);
                BaseId = (data[2] << 8) + data[3];
                BitRate = (CanBitRate)((data[1] & 0xF0)>>4);

                key = (BaseId, (int)MessagePrefix.Can, 0);
                if (queue.TryGetValue(key, out response!))
                {
                    response.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                break;

            case MessagePrefix.Inputs:
                index = Input.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumDigitalInputs)
                {
                    if (Inputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Inputs, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Outputs:
                index = Output.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumOutputs)
                {
                    if (Outputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Outputs, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.OutputsPwm:
                index = Output.ExtractIndex(data[1], prefix);

                if (index >= 0 && index < NumOutputs)
                {
                    if (Outputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.OutputsPwm, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.VirtualInputs:
                index = VirtualInput.ExtractIndex(data[2], prefix);
                
                if (index >= 0 && index < NumVirtualInputs)
                {
                    if (VirtualInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.VirtualInputs, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Flashers:
                index = Flasher.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumFlashers)
                {
                    if (Flashers[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Flashers, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Wiper:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.Wiper, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.WiperSpeed:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.WiperSpeed, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.WiperDelays:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.WiperDelays, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.StarterDisable:
                if (StarterDisable.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.StarterDisable, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.CanInputs:
                index = CanInput.ExtractIndex(data[1], prefix);
                if (index >= 0 && index < NumCanInputs)
                {
                    if(CanInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.CanInputs, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.CanInputsId:
                index = CanInput.ExtractIndex(data[1], prefix);
                if (index >= 0 && index < NumCanInputs)
                {
                    if (CanInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.CanInputsId, index);
                        if (queue.TryGetValue(key, out response!))
                        {
                            response.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Counter:
				index = Counter.ExtractIndex(data[1], prefix);
				if (index >= 0 && index < NumCounters)
				{
					if (Counters[index].Receive(data, prefix))
					{
						key = (BaseId, (int)MessagePrefix.Counter, index);
						if (queue.TryGetValue(key, out response!))
						{
							response.TimeSentTimer?.Dispose();
							queue.TryRemove(key, out _);
						}
					}
				}

				break;

			case MessagePrefix.Conditions:
				index = Condition.ExtractIndex(data[1], prefix);
				if (index >= 0 && index < NumConditions)
				{
					if (Conditions[index].Receive(data, prefix))
					{
						key = (BaseId, (int)MessagePrefix.Conditions, index);
						if (queue.TryGetValue(key, out response!))
						{
							response.TimeSentTimer?.Dispose();
							queue.TryRemove(key, out _);
						}
					}
				}

				break;

			case MessagePrefix.BurnSettings:
                if (data[1] == 1) //Successful burn
                {
                    //TODO: Add log
                    //Logger.Info($"{Name} ID: {BaseId}, Burn Successful");

                    key = (BaseId, (int)MessagePrefix.BurnSettings, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[1] == 0) //Unsuccessful burn
                {
                    //TODO: Add log
                    //Logger.Error($"{Name} ID: {BaseId}, Burn Failed");
                }
                break;

            case MessagePrefix.Sleep:
                if (data[1] == 1) //Successful sleep
                {
                    //TODO: Add log
                    //Logger.Info($"{Name} ID: {BaseId}, Sleep Successful");

                    key = (BaseId, (int)MessagePrefix.Sleep, 0);
                    if (queue.TryGetValue(key, out response!))
                    {
                        response.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[1] == 0) //Unsuccessful sleep
                {
                    //TODO: Add log
                    //Logger.Error($"{Name} ID: {BaseId}, Sleep Failed");
                }
                break;

            default:
                break;
        }
    }

    protected void ReadInfoWarnErrorMessage(byte[] data)
    {
        //Response is lowercase version of set/get prefix
        var type = (MessageType)char.ToUpper(Convert.ToChar(data[0]));
        var src = (MessageSrc)data[1];

        switch (type)
        {
            case MessageType.Info:
                //TODO: Add log
                //Logger.Info($"{Name} ID: {BaseId}, Src: {src} {((data[3] << 8) + data[2])} {((data[5] << 8) + data[4])} {((data[7] << 8) + data[6])}");
                break;
            case MessageType.Warning:
                //TODO: Add log
                //Logger.Warn($"{Name} ID: {BaseId}, Src: {src} {((data[3] << 8) + data[2])} {((data[5] << 8) + data[4])} {((data[7] << 8) + data[6])}");
                break;
            case MessageType.Error:
                //TODO: Add log
                //Logger.Error($"{Name} ID: {BaseId}, Src: {src} {((data[3] << 8) + data[2])} {((data[5] << 8) + data[4])} {((data[7] << 8) + data[6])}");
                break;
        }
    }

    public List<DeviceResponse> GetUploadMsgs()
    {
        var id = BaseId - 1;

        var msgs = new List<DeviceResponse>
        {
            //Request settings messages
            //Version
            new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Version,
                Index = 0,
                Data = new CanData
                {
                    Id = id,
                    Len = 1,
                    Payload = [Convert.ToByte(MessagePrefix.Version), 0, 0, 0, 0, 0, 0, 0]
                },
                MsgDescription="Version"
            },
            //CAN settings
            new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Data = new CanData
                {
                    Id = id,
                    Len = 1,
                    Payload = [Convert.ToByte(MessagePrefix.Can), 0, 0, 0, 0, 0, 0, 0]
                },
                MsgDescription="CANSettings"
            }
        };

        //Inputs
        foreach (var function in Inputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Inputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs
        foreach (var function in Outputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Outputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs PWM
        foreach (var function in Outputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.OutputsPwm);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Virtual inputs
        foreach (var function in Inputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.VirtualInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Flashers
        foreach (var function in Flashers)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Flashers);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs
        foreach (var function in CanInputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.CanInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs ID
        foreach (var function in CanInputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.CanInputsId);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Wiper
        var wiperMsg = Wipers.CreateUploadRequest(id, MessagePrefix.Wiper);
        if ( wiperMsg != null)
            msgs.Add(wiperMsg);

        //Wiper speeds
        var wiperSpeedMsg = Wipers.CreateUploadRequest(id, MessagePrefix.WiperSpeed);
        if ( wiperSpeedMsg != null)
            msgs.Add(wiperSpeedMsg);

        //Wiper delays
        var wiperDelayMsg = Wipers.CreateUploadRequest(id, MessagePrefix.WiperDelays);
        if ( wiperDelayMsg != null)
            msgs.Add(wiperDelayMsg);

        //Starter disable
        var starterDisableMsg = StarterDisable.CreateUploadRequest(id, MessagePrefix.StarterDisable);
        if ( starterDisableMsg != null)
            msgs.Add(starterDisableMsg);

		//Counter
        foreach (var function in Counters)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Counter);
            if ( msg != null)
                msgs.Add(msg);
        }

		//Condition
        foreach (var function in Conditions)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Conditions);
            if ( msg != null)
                msgs.Add(msg);
        }

		return msgs;
    }

    public List<DeviceResponse> GetDownloadMsgs()
    {
        var id = BaseId - 1;

        List<DeviceResponse> msgs =
        [
            new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Data = new CanData
                {
                    Id = id,
                    Len = 4,
                    Payload =
                    [
                        Convert.ToByte(MessagePrefix.Can), //Byte 0
                        Convert.ToByte(Convert.ToByte(SleepEnabled) +
                                       (Convert.ToByte(CanFiltersEnabled) << 1) +
                                       ((Convert.ToByte(BitRate) & 0x0F) << 4)),
                        Convert.ToByte((BaseId & 0xFF00) >> 8), //Byte 2
                        Convert.ToByte(BaseId & 0x00FF), //Byte 3
                        0, 0, 0, 0
                    ]
                },
                MsgDescription = "CANSettings"
            }
        ];

        //Inputs
        foreach(var function in Inputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Inputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs
        foreach(var function in Outputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Outputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs PWM
        foreach(var function in Outputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.OutputsPwm);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Virtual inputs
        foreach(var function in VirtualInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.VirtualInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Flashers
        foreach(var function in Flashers)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Flashers);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs
        foreach(var function in CanInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.CanInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs ID
        foreach(var function in CanInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.CanInputsId);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Wiper
        var wiperMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.Wiper);
        if ( wiperMsg != null)
            msgs.Add(wiperMsg);

        //Wiper speeds
        var wiperSpeedMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.WiperSpeed);
        if ( wiperSpeedMsg != null)
            msgs.Add(wiperSpeedMsg);

        //Wiper delays
        var wiperDelayMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.WiperDelays);
        if ( wiperDelayMsg != null)
            msgs.Add(wiperDelayMsg);

        //Starter disable
        var starterDisableMsg = StarterDisable.CreateDownloadRequest(id, MessagePrefix.StarterDisable);
        if ( starterDisableMsg != null)
            msgs.Add(starterDisableMsg);

		//Counter
        foreach(var function in Counters)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Counter);
            if ( msg != null)
                msgs.Add(msg);
        }

        foreach(var function in Conditions)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Conditions);
            if ( msg != null)
                msgs.Add(msg);
        }

		return msgs;
    }

    public List<DeviceResponse> GetUpdateMsgs(int newId)
    {
        var id = BaseId - 1;

        List<DeviceResponse> msgs =
        [
            new DeviceResponse
            {
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Data = new CanData
                {
                    Id = id,
                    Len = 4,
                    Payload =
                    [
                        Convert.ToByte(MessagePrefix.Can), //Byte 0
                        Convert.ToByte(Convert.ToByte(SleepEnabled) +
                                       (Convert.ToByte(CanFiltersEnabled) << 1) +
                                       ((Convert.ToByte(BitRate) & 0x0F) << 4)),
                        Convert.ToByte((newId & 0xFF00) >> 8), //Byte 2
                        Convert.ToByte(newId & 0x00FF), //Byte 3
                        0, 0, 0, 0
                    ]
                },
                MsgDescription = "CANSettings"
            }

        ];

        return msgs;
    }

    public DeviceResponse GetBurnMsg()
    {
        return new DeviceResponse
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.BurnSettings,
            Index = 0,
            Data = new CanData
            {
                Id = BaseId - 1,
                Len = 4,
                Payload = [Convert.ToByte(MessagePrefix.BurnSettings), 1, 3, 8, 0, 0, 0, 0]
            },
            MsgDescription = "Burn Settings"
        };
    }

    public DeviceResponse GetSleepMsg()
    {
        return new DeviceResponse
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Sleep,
            Index = 0,
            Data = new CanData
            {
                Id = BaseId - 1,
                Len = 5,
                Payload = [Convert.ToByte(MessagePrefix.Sleep), Convert.ToByte('Q'), Convert.ToByte('U'), Convert.ToByte('I'), Convert.ToByte('T'), 0, 0, 0
                ]
            },
            MsgDescription = "Sleep Request"
        };
    }

    public DeviceResponse GetVersionMsg()
    {
        return new DeviceResponse
        {
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Version,
            Index = 0,
            Data = new CanData
            {
                Id = BaseId - 1,
                Len = 1,
                Payload = [Convert.ToByte(MessagePrefix.Version), 0, 0, 0, 0, 0, 0, 0]
            },
            MsgDescription = "Version"
        };
    }
    
    protected bool CheckVersion(int major, int minor, int build)
    {
        if (major > MinMajorVersion)
            return true;

        if ((major == MinMajorVersion) && (minor > MinMinorVersion))
            return true;

        if ((major == MinMajorVersion) && (minor == MinMinorVersion) && (build >= MinBuildVersion))
            return true;
            
        return false;
    }
}