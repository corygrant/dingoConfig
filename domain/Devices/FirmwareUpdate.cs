using System.Buffers.Binary;
using System.Globalization;
using domain.Models;

namespace domain.Devices;

// Firmware update over CAN through the OpenBLT bootloader, dingoFW bootloader/common. XCP on base + 12 (to the
// module) and base + 13 (from it), one command per frame, the module's own bitrate. Same command sequence as
// OpenBLT's BootCommander (LibOpenBLT xcploader.c), plus a read-back verify before the new app is started.
public class FirmwareUpdate
{
    public const int XcpTxOffset = 12; // Host -> bootloader
    public const int XcpRxOffset = 13; // Bootloader -> host

    private const byte CmdConnect = 0xFF, CmdDisconnect = 0xFE, CmdSetMta = 0xF6, CmdUpload = 0xF5;
    private const byte CmdProgramStart = 0xD2, CmdProgramClear = 0xD1, CmdProgram = 0xD0;
    private const byte CmdProgramReset = 0xCF, CmdProgramMax = 0xC9;
    private const byte PidRes = 0xFF;

    private readonly SemaphoreSlim _busy = new(1, 1);
    private TaskCompletionSource<byte[]>? _pending;
    private Action<List<DeviceCanFrame>> _send = _ => { };
    private int _maxCto = 8, _maxDto = 8;

    public bool Busy => _busy.CurrentCount == 0;
    public string Status { get; private set; } = string.Empty;
    public long Done { get; private set; }
    public long Total { get; private set; } // 0 while there's nothing to count

    // Called with every frame on base + XcpRxOffset
    public void HandleReply(byte[] data) => Interlocked.Exchange(ref _pending, null)?.TrySetResult(data);

    public record Segment(uint Address, byte[] Data);

    // Motorola S-record data records (S1/S2/S3), merged into contiguous segments
    public static List<Segment> ParseSrec(string text)
    {
        var records = new SortedDictionary<uint, byte[]>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 4 || line[0] != 'S') continue;
            var addrLen = line[1] switch { '1' => 2, '2' => 3, '3' => 4, _ => 0 };
            if (addrLen == 0) continue;

            var bytes = Convert.FromHexString(line.AsSpan(2));
            if (bytes.Length != bytes[0] + 1 || bytes.Length < addrLen + 2)
                throw new FormatException($"Bad record length: {line}");
            byte sum = 0;
            foreach (var b in bytes) sum += b;
            if (sum != 0xFF)
                throw new FormatException($"Bad checksum: {line}");

            uint addr = 0;
            for (var i = 1; i <= addrLen; i++) addr = (addr << 8) | bytes[i];
            records[addr] = bytes[(addrLen + 1)..^1];
        }

        var segments = new List<Segment>();
        var cur = new List<byte>();
        uint start = 0;
        foreach (var (addr, data) in records)
        {
            if (cur.Count > 0 && addr != start + cur.Count)
            {
                segments.Add(new Segment(start, cur.ToArray()));
                cur.Clear();
            }
            if (cur.Count == 0) start = addr;
            cur.AddRange(data);
        }
        if (cur.Count > 0) segments.Add(new Segment(start, cur.ToArray()));
        if (segments.Count == 0)
            throw new FormatException("No data records, not an S-record file");
        return segments;
    }

    // Programs the image and starts it. Null on success, otherwise the reason it failed.
    // bootloaderMsg puts a running app into the bootloader, null when it's already there (recovery after a power cut
    // mid-update: the bootloader is on the default base id at 500K).
    public async Task<string?> FlashAsync(int baseId, List<Segment> image, DeviceCanFrame? bootloaderMsg,
        Action<List<DeviceCanFrame>> send, CancellationToken ct = default)
    {
        if (!await _busy.WaitAsync(0, ct))
            return "A firmware update is already running";
        _send = send;
        try
        {
            Begin("Entering bootloader", 0);
            if (bootloaderMsg != null)
            {
                send([bootloaderMsg]);
                await Task.Delay(500, ct); // App resets, bootloader comes up
            }

            Begin("Connecting", 0);
            byte[]? rsp = null;
            for (var i = 0; i < 20 && rsp == null; i++)
                rsp = await Command(baseId, [CmdConnect, 0], 250, ct);
            if (rsp == null || rsp.Length < 6)
                return $"No reply from the bootloader on 0x{baseId + XcpRxOffset:X3}";
            _maxCto = Math.Clamp((int)rsp[3], 2, 8);
            _maxDto = Math.Clamp(rsp[4] | (rsp[5] << 8), 2, 8); // Little endian, every dingo is an STM32

            rsp = await Command(baseId, [CmdProgramStart], 1000, ct);
            if (rsp == null)
                return "Bootloader refused PROGRAM_START";
            var progCto = rsp.Length >= 4 && rsp[3] != 0 ? Math.Min((int)rsp[3], _maxCto) : _maxCto;

            Begin("Erasing", 0);
            foreach (var seg in image)
                if (!await SetMta(baseId, seg.Address, ct) ||
                    await Command(baseId, Long(CmdProgramClear, (uint)seg.Data.Length), 20000, ct) == null)
                    return $"Erase failed at 0x{seg.Address:X8}";

            Begin("Writing", image.Sum(s => (long)s.Data.Length));
            foreach (var seg in image)
            {
                if (!await SetMta(baseId, seg.Address, ct))
                    return $"Write failed at 0x{seg.Address:X8}";
                var off = 0;
                foreach (var chunk in seg.Data.Chunk(progCto - 1))
                {
                    byte[] cmd = chunk.Length == progCto - 1
                        ? [CmdProgramMax, .. chunk]
                        : [CmdProgram, (byte)chunk.Length, .. chunk];
                    if (await Command(baseId, cmd, 1000, ct) == null)
                        return $"Write failed at 0x{seg.Address + off:X8}";
                    off += chunk.Length;
                    Done += chunk.Length;
                }
            }

            // PROGRAM with no data flushes the last blocks, the vector table goes last so the app only becomes
            // valid once everything else is written
            if (await Command(baseId, [CmdProgram, 0], 5000, ct) == null)
                return "Bootloader refused the end of programming";

            Begin("Verifying", image.Sum(s => (long)s.Data.Length));
            foreach (var seg in image)
            {
                if (!await SetMta(baseId, seg.Address, ct))
                    return $"Read back failed at 0x{seg.Address:X8}";
                for (var off = 0; off < seg.Data.Length;)
                {
                    var n = Math.Min(_maxDto - 1, seg.Data.Length - off);
                    rsp = await Command(baseId, [CmdUpload, (byte)n], 1000, ct);
                    if (rsp == null || rsp.Length < n + 1)
                        return $"Read back failed at 0x{seg.Address + off:X8}";
                    if (!rsp.AsSpan(1, n).SequenceEqual(seg.Data.AsSpan(off, n)))
                        return $"Verify failed at 0x{seg.Address + off:X8}, flash it again before power cycling the module";
                    off += n;
                    Done += n;
                }
            }

            Begin("Starting", 0);
            await Command(baseId, [CmdProgramReset], 200, ct); // Resets, a reply is optional
            return null;
        }
        catch (OperationCanceledException)
        {
            return "Cancelled";
        }
        finally
        {
            _pending = null;
            Status = string.Empty;
            _busy.Release();
        }
    }

    private void Begin(string status, long total)
    {
        Status = status;
        Done = 0;
        Total = total;
    }

    private async Task<bool> SetMta(int baseId, uint address, CancellationToken ct) =>
        await Command(baseId, Long(CmdSetMta, address), 1000, ct) != null;

    // [cmd, 0, 0, 0, value LE], SET_MTA address and PROGRAM_CLEAR length
    private static byte[] Long(byte cmd, uint value)
    {
        var p = new byte[8];
        p[0] = cmd;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(4), value);
        return p;
    }

    // Positive reply, null on timeout or an error packet. No resends: PROGRAM and UPLOAD move the address on, so a
    // resend after a lost reply would land in the wrong place. A failed update is simply run again.
    private async Task<byte[]?> Command(int baseId, byte[] cmd, int timeoutMs, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = tcs;
        var payload = new byte[8];
        cmd.CopyTo(payload, 0);
        _send([new DeviceCanFrame
        {
            SendOnly = true, // Replies are matched here, not in the DeviceManager queue
            DeviceBaseId = baseId,
            Frame = new CanFrame(Id: baseId + XcpTxOffset, Len: cmd.Length, Payload: payload),
            Name = "XCP"
        }]);
        if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, ct)) != tcs.Task)
        {
            ct.ThrowIfCancellationRequested();
            return null;
        }
        var rsp = tcs.Task.Result;
        return rsp.Length > 0 && rsp[0] == PidRes ? rsp : null;
    }
}
