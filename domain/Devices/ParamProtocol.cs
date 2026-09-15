using System.Collections.Concurrent;
using System.Threading;
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
    private int _readAllRetries;
    private bool _lastReadAllModified;
    private bool _lastWriteAllModified;
    private const int MaxBulkRetries = 3;
    public Action<string>? NotifySuccess;

    private readonly CumulativeCrc32 _writeCrc32 = new();
    private readonly CumulativeCrc32 _readCrc32 = new();

    // Targeted WriteAll recovery: on a failed full-WriteAll WriteAllComplete, firmware reports
    // exactly which params are missing instead of forcing a full resend. Only applies to full
    // WriteAll (see HandleWriteAllComplete) — WriteAllModified keeps the old log-and-stop
    // behavior since firmware can't tell "unmodified" from "dropped" for a host-chosen subset.
    private const int MaxWritePatchRounds = 2;
    private static readonly TimeSpan WriteAllOverallDeadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WriteMissingListTimeout = TimeSpan.FromMilliseconds(500);

    private readonly HashSet<(int Index, int SubIndex)> _pendingWriteMissing = new();
    private readonly object _writeLock = new();
    private int _writePatchRound;
    private bool _writeAllFullResendDone;
    private bool _awaitingWriteMissingList;
    private int _writeAwaitGeneration;
    private Timer? _writeMissingTimer;
    private DateTime _writeAllDeadlineAt;

    public void SetLogger(ILogger logger) => _logger = logger;

    public void HandleMessage(
        int baseId,
        int txId,
        string name,
        byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue,
        List<DeviceCanFrame> outgoing)
    {
        if (data.Length != 8) return;

        switch ((MessageCommand)data[0])
        {
            case MessageCommand.ReadParamNotFound:
            case MessageCommand.WriteAllParamNotFound:
            case MessageCommand.WriteAllOutOfRange:
                HandleParamError((MessageCommand)data[0], baseId, name, data, queue);
                break;

            case MessageCommand.Read:
            case MessageCommand.Write:
            case MessageCommand.WriteAllVal:
                HandleParamValue(baseId, data, queue);
                break;

            case MessageCommand.ReadAll:
            case MessageCommand.ReadAllModified:
                HandleReadAllStart((MessageCommand)data[0], baseId, name, data, queue);
                break;

            case MessageCommand.ReadAllRsp:
                HandleReadAllRsp(baseId, name, data);
                break;

            case MessageCommand.ReadAllComplete:
                HandleReadAllComplete(baseId, txId, name, data, outgoing);
                break;

            case MessageCommand.CheckCrcRsp:
                HandleCheckCrcRsp(baseId, name, data);
                break;

            case MessageCommand.WriteAll:
            case MessageCommand.WriteAllModified:
                HandleWriteAllStart((MessageCommand)data[0], baseId, txId, name, data, queue, outgoing);
                break;

            case MessageCommand.WriteAllComplete:
                HandleWriteAllComplete(baseId, txId, name, data, outgoing);
                break;

            case MessageCommand.WriteAllMissing:
                HandleWriteAllMissing(data);
                break;

            case MessageCommand.WriteAllMissingDone:
                HandleWriteAllMissingDone(baseId, txId, name, data, outgoing);
                break;

            case MessageCommand.BurnParams:
                HandleBurnParams(baseId, name, data, queue);
                break;

            case MessageCommand.Sleep:
                HandleSleep(baseId, name, data, queue);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private static void ClearQueuedRequest(
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue,
        int baseId, int index, int subIndex)
    {
        var key = (baseId, index, subIndex);
        if (queue.TryGetValue(key, out var canFrame))
        {
            canFrame.TimeSentTimer?.Dispose();
            queue.TryRemove(key, out _);
        }
    }

    private static object ConvertWireValue(byte[] data, DeviceParameter param)
    {
        if (param.ValueType == typeof(double))
            return DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isFloat: true);

        var rawValue = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 32, isSigned: param.IsSignedInt);

        return param.ValueType switch
        {
            { } t when t == typeof(bool) => rawValue != 0,
            { } t when t == typeof(int) => (int)rawValue,
            { IsEnum: true } t => Enum.ToObject(t, (int)rawValue),
            _ => rawValue
        };
    }

    private static DeviceCanFrame BuildCheckCrcFrame(int baseId, int txId) => new()
    {
        DeviceBaseId = baseId,
        SendOnly = true,
        Frame = new CanFrame(Id: txId, Len: 8, Payload: [Convert.ToByte(MessageCommand.CheckCrc), 0, 0, 0, 0, 0, 0, 0]),
        Name = "CheckCRC"
    };

    // ------------------------------------------------------------------
    // Per-command handlers
    // ------------------------------------------------------------------

    private void HandleParamError(MessageCommand cmd, int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        var matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);
        var paramName = matchingParam?.Name ?? "";

        ClearQueuedRequest(queue, baseId, index, subIndex);

        var errorType = cmd switch
        {
            MessageCommand.ReadParamNotFound => "Read Param Not Found",
            MessageCommand.WriteAllParamNotFound => "Write Param Not Found",
            MessageCommand.WriteAllOutOfRange => "Write Param Out of Range",
            _ => "Invalid error type"
        };

        _logger.LogError("{Name} ID: {BaseId}, {ErrorType} - {paramName} - 0x{index:X}:{subindex}",
            name, baseId, errorType, paramName, index, subIndex);
    }

    private void HandleParamValue(int baseId, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        var matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);
        if (matchingParam is null) return;

        matchingParam.SetValue(ConvertWireValue(data, matchingParam));

        ClearQueuedRequest(queue, baseId, index, subIndex);
    }

    private void HandleReadAllStart(MessageCommand cmd, int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        _lastReadAllModified = cmd == MessageCommand.ReadAllModified;

        _readCrc32.Reset();

        _tempParamValues.Clear();
        foreach (var param in @params)
            _tempParamValues[(param.Index, param.SubIndex)] = param.DefaultValue;

        _readAllCount = 0;

        ClearQueuedRequest(queue, baseId, index, subIndex);

        _logger.LogInformation("{Name} ID: {BaseId}, Read All Started", name, baseId);
    }

    private void HandleReadAllRsp(int baseId, string name, byte[] data)
    {
        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        var matchingParam = @params.FirstOrDefault(p => p.Index == index && p.SubIndex == subIndex);
        if (matchingParam is null)
        {
            _logger.LogWarning("{Name} ID: {BaseId}, Cannot find param {index}:{subIndex}", name, baseId, index, subIndex);
            return;
        }

        _tempParamValues[(index, subIndex)] = ConvertWireValue(data, matchingParam);
        _readCrc32.Update(data.Skip(4).Take(4).ToArray());
        _readAllCount++;
    }

    private void HandleReadAllComplete(int baseId, int txId, string name, byte[] data, List<DeviceCanFrame> outgoing)
    {
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
            _readAllRetries = 0;
            _logger.LogInformation("{Name} ID: {BaseId}, Read All Complete {pdmCrc} = {thisCrc}, {fromPdm}",
                name, baseId, readAllCrc, _readCrc32.Final, readAllCount);
            NotifySuccess?.Invoke($"{name}: Read Successful");
        }
        else
        {
            _tempParamValues.Clear();
            _logger.LogError("{Name} ID: {BaseId}, Read All Incomplete {pdmCrc} != {thisCrc}, {fromPdm} vs {received}",
                                name, baseId, readAllCrc, _readCrc32.Final, readAllCount, _readAllCount);

            if (_readAllRetries < MaxBulkRetries)
            {
                _readAllRetries++;
                _logger.LogWarning("{Name} ID: {BaseId}, Retrying Read All ({Attempt}/{Max})",
                    name, baseId, _readAllRetries, MaxBulkRetries);

                outgoing.Add(new DeviceCanFrame
                {
                    DeviceBaseId = baseId,
                    SendOnly = true,
                    Frame = new CanFrame(Id: txId, Len: 8, Payload: [Convert.ToByte(_lastReadAllModified ? MessageCommand.ReadAllModified : MessageCommand.ReadAll), 0, 0, 0, 0, 0, 0, 0]),
                    Name = "ReadAll (retry)"
                });
                return;
            }

            _readAllRetries = 0;
            _logger.LogError("{Name} ID: {BaseId}, Read All failed after {Max} retries", name, baseId, MaxBulkRetries);
        }

        outgoing.Add(BuildCheckCrcFrame(baseId, txId));
    }

    private void HandleCheckCrcRsp(int baseId, string name, byte[] data)
    {
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
    }

    private void HandleWriteAllStart(MessageCommand cmd, int baseId, int txId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue,
        List<DeviceCanFrame> outgoing)
    {
        bool isModified = cmd == MessageCommand.WriteAllModified;

        _lastWriteAllModified = isModified;
        _writeCrc32.Reset();
        ResetWritePatchState();

        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        ClearQueuedRequest(queue, baseId, index, subIndex);

        outgoing.AddRange(BuildWriteAllMsgs(baseId, txId, allParams: !isModified));

        if (isModified)
            _logger.LogInformation("{Name} ID: {BaseId}, Write All Modified Started {Count}", name, baseId, _writeAllCount);
        else
            _logger.LogInformation("{Name} ID: {BaseId}, Write All Started {Count}", name, baseId, _writeAllCount);
    }

    private void HandleWriteAllComplete(int baseId, int txId, string name, byte[] data, List<DeviceCanFrame> outgoing)
    {
        var writeAllCount = data[2] << 8 | data[1];
        var writeAllApplied = data[3] == 1;
        uint writeAllCrc = (uint)(data[7] << 24 | data[6] << 16 | data[5] << 8 | data[4]);

        if (writeAllApplied)
        {
            ResetWritePatchState();
            _logger.LogInformation("{Name} ID: {BaseId}, Write All Completed {pdmCrc} = {thisCrc}, {fromPdm}",
                name, baseId, writeAllCrc, _writeCrc32.Final, writeAllCount);
            NotifySuccess?.Invoke($"{name}: Write Successful");

            outgoing.Add(BuildCheckCrcFrame(baseId, txId));
            return;
        }

        if (_lastWriteAllModified)
        {
            // WriteAllModified sends a host-chosen subset — firmware can't tell
            // "unmodified" from "dropped" for it, so there's no targeted recovery here,
            // same as before this change.
            _logger.LogError("{Name} ID: {BaseId}, Write All Failed {pdmCrc} != {thisCrc}, PDM Sent:{fromPdm} vs Received:{received}",
                name, baseId, writeAllCrc, _writeCrc32.Final, writeAllCount, _writeAllCount);

            outgoing.Add(BuildCheckCrcFrame(baseId, txId));
            return;
        }

        // Full WriteAll: don't give up yet. Firmware follows a failed WriteAllComplete with a
        // WriteAllMissing/WriteAllMissingDone report of exactly what's missing (see
        // HandleWriteAllMissing/HandleWriteAllMissingDone for the bounded patch/fallback/give-up
        // sequence). _writeMissingTimer is a backstop in case that report (or firmware's support
        // for it) never shows up.
        _logger.LogError("{Name} ID: {BaseId}, Write All Failed {pdmCrc} != {thisCrc}, PDM Sent:{fromPdm} vs Received:{received} — awaiting missing-param report",
            name, baseId, writeAllCrc, _writeCrc32.Final, writeAllCount, _writeAllCount);

        lock (_writeLock)
        {
            _awaitingWriteMissingList = true;
            var generation = ++_writeAwaitGeneration;
            _writeMissingTimer?.Dispose();
            _writeMissingTimer = new Timer(_ => OnWriteMissingListTimeout(name, baseId, generation),
                null, WriteMissingListTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private void HandleWriteAllMissing(byte[] data)
    {
        int index = data[2] << 8 | data[1];
        int subIndex = data[3];

        lock (_writeLock)
        {
            if (_awaitingWriteMissingList)
                _pendingWriteMissing.Add((index, subIndex));
        }
    }

    private void HandleWriteAllMissingDone(int baseId, int txId, string name, byte[] data, List<DeviceCanFrame> outgoing)
    {
        var missingCount = data[2] << 8 | data[1];
        bool wasStale;
        HashSet<(int Index, int SubIndex)> missingSnapshot;

        lock (_writeLock)
        {
            _writeMissingTimer?.Dispose();
            _writeMissingTimer = null;

            wasStale = !_awaitingWriteMissingList;
            _awaitingWriteMissingList = false;
            _writeAwaitGeneration++;

            missingSnapshot = new HashSet<(int, int)>(_pendingWriteMissing);
            _pendingWriteMissing.Clear();
        }

        // Belongs to an attempt we already abandoned (deadline/round fallback already fired, or
        // a fresh WriteAll started) — a new WriteAll always resets this flag, so a late arrival
        // here can't be misapplied to whatever is happening now.
        if (wasStale) return;

        var needsFullResend = missingCount == 0xFFFF
            || _writePatchRound >= MaxWritePatchRounds
            || DateTime.Now > _writeAllDeadlineAt;

        if (needsFullResend)
        {
            if (!_writeAllFullResendDone)
            {
                _writeAllFullResendDone = true;
                _logger.LogWarning("{Name} ID: {BaseId}, Write All missing-param patch exhausted, falling back to one full resend",
                    name, baseId);
                // BuildWriteAllMsgs accumulates onto _writeCrc32 rather than resetting it, so it
                // must be reset here exactly like HandleWriteAllStart does — otherwise this
                // recomputed CRC would include the abandoned first attempt's bytes too.
                _writeCrc32.Reset();
                outgoing.AddRange(BuildWriteAllMsgs(baseId, txId, allParams: true));
            }
            else
            {
                _logger.LogError("{Name} ID: {BaseId}, Write All failed — could not converge after patch rounds and a full resend",
                    name, baseId);
            }
            return;
        }

        _writePatchRound++;

        foreach (var parameter in @params.Where(p => missingSnapshot.Contains((p.Index, p.SubIndex))))
        {
            outgoing.Add(new DeviceCanFrame
            {
                DeviceBaseId = baseId,
                SendOnly = true,
                Frame = ParamCodec.ToFrame(MessageCommand.WriteAllVal, parameter, txId),
                Name = parameter.Name
            });
        }

        outgoing.Add(BuildWriteAllCompleteFrame(baseId, txId));
    }

    private void HandleBurnParams(int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        if (data[4] == 1) //Successful burn
        {
            _logger.LogInformation("{Name} ID: {BaseId}, Burn Successful", name, baseId);
            NotifySuccess?.Invoke($"{name}: Burn Successful");

            ClearQueuedRequest(queue, baseId, 3 << 8 | 1, 8); //Index bytes are 1 and 3, subindex is 8
        }

        if (data[4] == 0) //Unsuccessful burn
            _logger.LogError("{Name} ID: {BaseId}, Burn Failed", name, baseId);
    }

    private void HandleSleep(int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        if (data[5] == 1) //Successful sleep
        {
            _logger.LogInformation("{Name} ID: {BaseId}, Sleep Successful", name, baseId);
            NotifySuccess?.Invoke($"{name}: Sleep Successful");

            ClearQueuedRequest(queue, baseId, 'U' << 8 | 'Q', 'I'); //Index bytes = QU, Subindex = I
        }

        if (data[5] == 0) //Unsuccessful sleep
            _logger.LogError("{Name} ID: {BaseId}, Sleep Failed", name, baseId);
    }

    // ------------------------------------------------------------------
    // WriteAll helpers
    // ------------------------------------------------------------------

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

        //Write all complete, with num params and the CRC we computed so the
        //firmware can reject the batch if what it received doesn't match.
        msgs.Add(BuildWriteAllCompleteFrame(baseId, txId));

        return msgs;
    }

    // Rebuilds the WriteAllComplete frame from the count/CRC of the *original* full WriteAll
    // (_writeAllCount / _writeCrc32 aren't touched again until the next WriteAll/WriteAllModified
    // starts), so a missing-param patch round can resend it unchanged rather than recomputing
    // anything — the set of values firmware is expected to end up with hasn't changed.
    private DeviceCanFrame BuildWriteAllCompleteFrame(int baseId, int txId)
    {
        uint expectedWriteCrc = _writeCrc32.Final;
        return new DeviceCanFrame
        {
            DeviceBaseId = baseId,
            SendOnly = true,
            Frame = new CanFrame(
                Id: txId,
                Len: 8,
                Payload: [  Convert.ToByte(MessageCommand.WriteAllComplete),
                    Convert.ToByte(_writeAllCount & 0xFF),
                    Convert.ToByte((_writeAllCount >> 8) & 0xFF),
                    0,
                    Convert.ToByte(expectedWriteCrc & 0xFF),
                    Convert.ToByte((expectedWriteCrc >> 8) & 0xFF),
                    Convert.ToByte((expectedWriteCrc >> 16) & 0xFF),
                    Convert.ToByte((expectedWriteCrc >> 24) & 0xFF)]),
            Name = "WriteAllComplete"
        };
    }

    private void ResetWritePatchState()
    {
        lock (_writeLock)
        {
            _writeMissingTimer?.Dispose();
            _writeMissingTimer = null;
            _awaitingWriteMissingList = false;
            _writeAwaitGeneration++;
            _pendingWriteMissing.Clear();
        }

        _writePatchRound = 0;
        _writeAllFullResendDone = false;
        _writeAllDeadlineAt = DateTime.Now + WriteAllOverallDeadline;
    }

    // Timer callback (runs on the ThreadPool, not the RX pipeline thread) — fires only if
    // WriteAllMissingDone never arrives after a failed full-WriteAll WriteAllComplete (report
    // itself lost, or firmware doesn't support it). Guards on the generation captured when the
    // timer was created, not on _awaitingWriteMissingList directly: Timer.Dispose() doesn't stop
    // an already-in-flight callback, so without this a stale callback from an abandoned attempt
    // (or an earlier patch round within the same attempt) could otherwise clear the flag — and
    // thus discard the real report — for a *later* wait that's already reusing it. Every place
    // that starts, resolves, or resets a wait bumps the generation, so a stale callback can never
    // match. Only logs: there's no outgoing frame list to append to from a background thread
    // here, so this deliberately fails loud-and-fast rather than silently hanging — the user can
    // retry, which cleanly resets this state.
    private void OnWriteMissingListTimeout(string name, int baseId, int generation)
    {
        lock (_writeLock)
        {
            if (generation != _writeAwaitGeneration) return; // superseded by a reset or a new attempt
            _awaitingWriteMissingList = false;
            _pendingWriteMissing.Clear();
        }

        _logger.LogError("{Name} ID: {BaseId}, Write All missing-param report timed out — firmware may not support it, or the report was lost",
            name, baseId);
    }

    private uint CalcCrc()
    {
        CumulativeCrc32 checkCrc32 = new();

        foreach (var parameter in @params)
        {
            //Always use all parameters to check CRC
            var data = ParamCodec.ToFrame(MessageCommand.Null, parameter, 0);
            checkCrc32.Update(data.Payload.Skip(4).Take(4).ToArray());
        }

        return checkCrc32.Final;
    }
}
