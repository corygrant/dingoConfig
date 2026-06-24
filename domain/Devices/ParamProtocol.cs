using System.Collections.Concurrent;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace domain.Devices;

internal class ParamProtocol(IDeviceConfigurable device, List<DeviceParameter> @params)
{
    private ILogger _logger = NullLogger.Instance;

    private readonly Dictionary<(int Index, int SubIndex), object> _tempParamValues = new();
    private int _readAllCount;
    private int _writeAllCount;
    public Action<string>? NotifySuccess;
    
    private readonly CumulativeCrc32 _writeCrc32 =  new();
    private readonly CumulativeCrc32 _readCrc32 =  new();

    public void SetLogger(ILogger logger) => _logger = logger;

    public void HandleMessage(
        int baseId,
        int txId,
        string name,
        byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue,
        List<DeviceCanFrame> outgoing)
    {
        DeviceCanFrame canFrame;
        int index, subIndex;
        DeviceParameter? matchingParam;
        double rawValue;
        object convertedValue;
        (int BaseId, int, int) key;

        switch ((MessageCommand)data[0])
        {
            //Error message commands
            case MessageCommand.ReadParamNotFound:
            case MessageCommand.WriteAllParamNotFound:
            case MessageCommand.WriteAllOutOfRange:
                if (data.Length != 8) return;

                index = data[2] << 8 | data[1];
                subIndex = data[3];

                matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);

                var paramName = "";
                if (matchingParam != null)
                {
                    paramName = matchingParam.Name;
                }

                key = (baseId, index, subIndex);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                var errorType = (MessageCommand)data[0] switch
                {
                    MessageCommand.ReadParamNotFound => "Read Param Not Found",
                    MessageCommand.WriteAllParamNotFound => "Write Param Not Found",
                    MessageCommand.WriteAllOutOfRange => "Write Param Out of Range",
                    _ => "Invalid error type"
                };

                _logger.LogError("{Name} ID: {BaseId}, {ErrorType} - {paramName} - 0x{index:X}:{subindex}",
                    name, baseId, errorType, paramName, index, subIndex);

                break;

            case MessageCommand.Read:
            case MessageCommand.Write:
            case MessageCommand.WriteAllVal:
                if (data.Length != 8) return;

                index = data[2] << 8 | data[1];
                subIndex = data[3];

                matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);
                if (matchingParam is null) break;

                if (matchingParam.ValueType == typeof(double))
                {
                    convertedValue = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isFloat: true);
                }
                else
                {
                    rawValue = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isSigned: matchingParam.IsSignedInt);

                    // Convert to the appropriate type based on param.ValueType
                    convertedValue = matchingParam.ValueType switch
                    {
                        { } t when t == typeof(bool) => rawValue != 0,
                        { } t when t == typeof(int) => (int)rawValue,
                        { IsEnum: true } t => Enum.ToObject(t, (int)rawValue),
                        _ => rawValue
                    };
                }

                matchingParam.SetValue(convertedValue);

                key = (baseId, index, subIndex);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                break;

            case MessageCommand.ReadAll:
            case MessageCommand.ReadAllModified:
                if (data.Length != 8) return;

                index = data[2] << 8 | data[1];
                subIndex = data[3];

                _readCrc32.Reset();

                _tempParamValues.Clear();
                foreach (var param in @params)
                    _tempParamValues[(param.Index, param.SubIndex)] = param.DefaultValue;

                _readAllCount = 0;

                key = (baseId, index, subIndex);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                _logger.LogInformation("{Name} ID: {BaseId}, Read All Started", name, baseId);

                break;

            case MessageCommand.ReadAllRsp:
                if (data.Length != 8) return;

                index = data[2] << 8 | data[1];
                subIndex = data[3];

                matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);
                if (matchingParam is null)
                {
                    _logger.LogWarning("{Name} ID: {BaseId}, Cannot find param {index}:{subIndex}", name, baseId, index, subIndex);
                    break;
                }

                if (matchingParam.ValueType == typeof(double))
                {
                    convertedValue = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isFloat: true);
                }
                else
                {
                    rawValue = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isSigned: matchingParam.IsSignedInt);

                    // Convert to the appropriate type based on param.ValueType
                    convertedValue = matchingParam.ValueType switch
                    {
                        { } t when t == typeof(bool) => rawValue != 0,
                        { } t when t == typeof(int) => (int)rawValue,
                        { IsEnum: true } t => Enum.ToObject(t, (int)rawValue),
                        _ => rawValue
                    };
                }

                _tempParamValues[(index, subIndex)] = convertedValue;

                _readCrc32.Update(data.Skip(4).Take(4).ToArray());
                
                _readAllCount++;

                break;

            case MessageCommand.ReadAllComplete:
                if (data.Length != 8) return;

                var readAllCount = data[2] << 8 | data[1];
                uint readAllCrc = (uint)(data[7] << 24 | data[6] << 16 | data[5] << 8 | data[4]);

                if (readAllCrc == _readCrc32.Final)
                {
                    // End of params, apply all temporary values to actual properties
                    foreach (var param in @params)
                    {
                        var paramKey = (param.Index, param.SubIndex);
                        if (_tempParamValues.TryGetValue(paramKey, out var value))
                        {
                            param.SetValue(value);
                        }
                    }

                    _tempParamValues.Clear();
                    _logger.LogInformation("{Name} ID: {BaseId}, Read All Complete {pdmCrc} = {thisCrc}, {fromPdm}", 
                        name, baseId, readAllCrc, _readCrc32.Final, readAllCount);
                    NotifySuccess?.Invoke($"{name}: Read Successful");
                }
                else
                {
                    _tempParamValues.Clear();
                    _logger.LogError("{Name} ID: {BaseId}, Read All Incomplete {pdmCrc} != {thisCrc}, {fromPdm} vs {received}",
                                        name, baseId, readAllCrc, _readCrc32.Final, readAllCount, _readAllCount);
                }

                outgoing.Add(new DeviceCanFrame
                {
                    DeviceBaseId = baseId,
                    SendOnly = true,
                    Frame = new CanFrame(Id: txId, Len: 8, Payload: [Convert.ToByte(MessageCommand.CheckCrc), 0, 0, 0, 0, 0, 0, 0]),
                    Name = "CheckCRC"
                });
                
                break;
                
            case MessageCommand.CheckCrcRsp:
                if (data.Length != 8) return;
                
                uint checkCrc = (uint)(data[7] << 24 | data[6] << 16 | data[5] << 8 | data[4]);
                
                var thisCheck = CalcCrc();
                
                device.ConfigMismatch = checkCrc != thisCheck;
                if (!device.ConfigMismatch)
                    _logger.LogInformation("{Name} ID: {BaseId}, Config Matches {pdmCrc}", name, baseId, checkCrc);
                else
                {
                    _logger.LogWarning("{Name} ID: {BaseId}, Config Does Not Match {pdmCrc} != {thisCrc}", 
                        name, baseId, checkCrc, thisCheck);
                }

                break;

            case MessageCommand.WriteAll:
                if (data.Length != 8) return;
                
                _writeCrc32.Reset();

                index = data[2] << 8 | data[1];
                subIndex = data[3];

                key = (baseId, index, subIndex);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                //Write all values
                outgoing.AddRange(BuildWriteAllMsgs(baseId, txId, allParams: true));

                _logger.LogInformation("{Name} ID: {BaseId}, Write All Started {Count}", name, baseId, _writeAllCount);

                break;
            
            case MessageCommand.WriteAllModified:
                if (data.Length != 8) return;

                _writeCrc32.Reset();
                
                index = data[2] << 8 | data[1];
                subIndex = data[3];

                key = (baseId, index, subIndex);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                //Write all modified values
                outgoing.AddRange(BuildWriteAllMsgs(baseId, txId, allParams: false));

                _logger.LogInformation("{Name} ID: {BaseId}, Write All Modified Started {Count}", name, baseId, _writeAllCount);

                break;

            case MessageCommand.WriteAllComplete:
                if (data.Length != 8) return;
                
                var writeAllCount = data[2] << 8 | data[1];
                uint writeAllCrc = (uint)(data[7] << 24 | data[6] << 16 | data[5] << 8 | data[4]);

                if (writeAllCrc == _writeCrc32.Final)
                {
                    _logger.LogInformation("{Name} ID: {BaseId}, Write All Completed {pdmCrc} = {thisCrc}, {fromPdm}", 
                        name, baseId, writeAllCrc, _writeCrc32.Final, writeAllCount);
                    NotifySuccess?.Invoke($"{name}: Write Successful");
                }
                else
                {
                    _logger.LogError("{Name} ID: {BaseId}, Write All Failed {pdmCrc} != {thisCrc}, {fromPdm} vs {received}",
                        name, baseId, writeAllCrc, _writeCrc32.Final, writeAllCount, _writeAllCount);
                }
                
                outgoing.Add(new DeviceCanFrame
                {
                    DeviceBaseId = baseId,
                    SendOnly = true,
                    Frame = new CanFrame(Id: txId, Len: 8, Payload: [Convert.ToByte(MessageCommand.CheckCrc), 0, 0, 0, 0, 0, 0, 0]),
                    Name = "CheckCRC"
                });
                break;

		    case MessageCommand.BurnParams:
                if (data.Length != 8) return;

                if (data[4] == 1) //Successful burn
                {
                    _logger.LogInformation("{Name} ID: {BaseId}, Burn Successful", name, baseId);
                    NotifySuccess?.Invoke($"{name}: Burn Successful");

                    key = (baseId, 3 << 8 | 1, 8); //Index bytes are 1 and 3, subindex is 8
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[4] == 0) //Unsuccessful burn
                    _logger.LogError("{Name} ID: {BaseId}, Burn Failed", name, baseId);

                break;

            case MessageCommand.Sleep:
                if (data.Length != 8) return;

                if (data[5] == 1) //Successful sleep
                {
                    _logger.LogInformation("{Name} ID: {BaseId}, Sleep Successful", name, baseId);
                    NotifySuccess?.Invoke($"{name}: Sleep Successful");

                    key = (baseId, 'U' << 8 | 'Q', 'I'); //Index bytes = QU, Subindex = I
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[5] == 0) //Unsuccessful sleep
                    _logger.LogError("{Name} ID: {BaseId}, Sleep Failed", name, baseId);

                break;
        }
    }

    private List<DeviceCanFrame> BuildWriteAllMsgs(int baseId, int txId, bool allParams)
    {
        var writeParams = allParams ? @params : @params.Where(p => p.IsModified).ToList();
        
        List<DeviceCanFrame> msgs = [];
        _writeAllCount = writeParams.Count;

        foreach (var parameter in writeParams)
        {
            msgs.Add(new DeviceCanFrame
            {
                DeviceBaseId = baseId,
                SendOnly = true,
                Frame = ParamCodec.ToFrame(MessageCommand.WriteAllVal, parameter, txId),
                Name = parameter.Name
            });
            
            _writeCrc32.Update(msgs.Last().Frame.Payload.Skip(4).Take(4).ToArray());
        }

        //Write all complete, with num params
        msgs.Add(new DeviceCanFrame
        {
            DeviceBaseId = baseId,
            SendOnly = true,
            Frame = new CanFrame(
                Id: txId,
                Len: 8,
                Payload: [  Convert.ToByte(MessageCommand.WriteAllComplete),
                    Convert.ToByte(_writeAllCount & 0xFF),
                    Convert.ToByte((_writeAllCount >> 8) & 0xFF),
                    0, 0, 0, 0, 0]),
            Name = "WriteAllComplete"
        });

        return msgs;
    }

    private uint CalcCrc()
    {
        CumulativeCrc32 checkCrc32 =  new();
        
        foreach (var parameter in @params)
        {
            //Always use all parameters to check CRC
            var data = ParamCodec.ToFrame(MessageCommand.Null, parameter, 0);
            checkCrc32.Update(data.Payload.Skip(4).Take(4).ToArray());
        }
        
        return checkCrc32.Final;
    }
}
