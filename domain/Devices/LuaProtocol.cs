using System.Collections.Concurrent;
using System.Text;
using domain.Enums;
using domain.Models;

namespace domain.Devices;

// Lua script transfer, firmware core/lua_script.cpp CheckLuaMsgs. Requests on the config TX id, replies on the
// config RX id: [cmd, offHi, offLo, 5 bytes]. Requests go out a window at a time (the firmware's RX mailbox is
// small), unanswered ones are resent. Writes are addressed by offset, so a resend is harmless.
public class LuaProtocol
{
    private const int ChunkSize = 5;
    private const int Window = 8;
    private const int Retries = 5;
    private const int MaxLength = 0xFFFF; // 16-bit offset
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(300);

    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly ConcurrentDictionary<(int Cmd, int Offset), TaskCompletionSource<byte[]>> _pending = new();

    public bool Busy => _busy.CurrentCount == 0;
    public string Status { get; private set; } = string.Empty;
    public int Done { get; private set; }
    public int Total { get; private set; } // 0 while the length isn't known

    // Called with every frame on the config RX id
    public void HandleReply(byte[] data)
    {
        if (data.Length < 3 || data[0] < (byte)MessageCommand.LuaWrite || data[0] > (byte)MessageCommand.LuaErr)
            return;
        if (_pending.TryRemove((data[0], (data[1] << 8) | data[2]), out var tcs))
            tcs.TrySetResult(data);
    }

    // Writes, stores and reads back the script. Null on success, otherwise the reason it failed.
    public Task<string?> UploadAsync(int baseId, string script, Action<List<DeviceCanFrame>> send, CancellationToken ct = default) =>
        Run(async () =>
        {
            var bytes = Encoding.UTF8.GetBytes(script);
            if (bytes.Length > MaxLength)
                return $"Script is {bytes.Length} bytes, the limit is {MaxLength}";

            Begin("Writing", bytes.Length);
            var chunks = bytes.Chunk(ChunkSize).Select((c, i) => (i * ChunkSize, c)).ToList();
            if (await Exchange(baseId, MessageCommand.LuaWrite, chunks, send, ct) == null)
                return "No reply while writing the script";

            Begin("Storing", 0);
            var rsp = await Exchange(baseId, MessageCommand.LuaWriteComplete, [(bytes.Length, [])], send, ct);
            if (rsp == null)
                return "No reply to write complete";
            if (rsp[bytes.Length].Length < 4 || rsp[bytes.Length][3] != 1)
                return "Device failed to store the script";

            return await VerifyBytes(baseId, bytes, send, ct);
        });

    // Compares the stored script with the given one. Null when they match.
    public Task<string?> VerifyAsync(int baseId, string script, Action<List<DeviceCanFrame>> send, CancellationToken ct = default) =>
        Run(() => VerifyBytes(baseId, Encoding.UTF8.GetBytes(script), send, ct));

    // Stored script, null on timeout
    public Task<string?> ReadAsync(int baseId, Action<List<DeviceCanFrame>> send, CancellationToken ct = default) =>
        Run(async () =>
        {
            Begin("Reading", 0);
            var bytes = await ReadUntilNull(baseId, MessageCommand.LuaRead, send, ct);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        });

    // Last print()/luaLog() line or script error, null on timeout
    public Task<string?> ReadMessageAsync(int baseId, Action<List<DeviceCanFrame>> send, CancellationToken ct = default) =>
        Run(async () =>
        {
            Begin("Reading message", 0);
            var bytes = await ReadUntilNull(baseId, MessageCommand.LuaErr, send, ct);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        });

    private async Task<string?> VerifyBytes(int baseId, byte[] expected, Action<List<DeviceCanFrame>> send, CancellationToken ct)
    {
        Begin("Verifying", expected.Length);
        var stored = await ReadUntilNull(baseId, MessageCommand.LuaRead, send, ct);
        if (stored == null)
            return "No reply while reading the script back";
        if (stored.Length != expected.Length)
            return $"Device holds {stored.Length} bytes, expected {expected.Length}";
        for (var i = 0; i < stored.Length; i++)
            if (stored[i] != expected[i])
                return $"Script differs from the device at byte {i}";
        return null;
    }

    private async Task<T> Run<T>(Func<Task<T>> op)
    {
        if (!await _busy.WaitAsync(0))
            throw new InvalidOperationException("A Lua transfer is already running");
        try
        {
            return await op();
        }
        finally
        {
            _pending.Clear();
            Status = string.Empty;
            _busy.Release();
        }
    }

    private void Begin(string status, int total)
    {
        Status = status;
        Done = 0;
        Total = total;
    }

    // The firmware returns 0 past the end, the source text never holds a 0
    private async Task<byte[]?> ReadUntilNull(int baseId, MessageCommand cmd, Action<List<DeviceCanFrame>> send, CancellationToken ct)
    {
        var buf = new List<byte>();
        for (var off = 0; off < MaxLength; off += ChunkSize * Window)
        {
            var reqs = Enumerable.Range(0, Window)
                .Select(i => off + i * ChunkSize)
                .Where(o => o <= MaxLength)
                .Select(o => (o, Array.Empty<byte>()))
                .ToList();
            var replies = await Exchange(baseId, cmd, reqs, send, ct);
            if (replies == null)
                return null;

            foreach (var (o, _) in reqs)
            {
                var d = replies[o];
                for (var i = 3; i < 3 + ChunkSize; i++)
                {
                    if (i >= d.Length || d[i] == 0)
                        return buf.ToArray();
                    buf.Add(d[i]);
                }
            }
        }
        return buf.ToArray();
    }

    // Replies by offset, null when a window goes unanswered after every retry
    private async Task<Dictionary<int, byte[]>?> Exchange(int baseId, MessageCommand cmd,
        IReadOnlyList<(int Offset, byte[] Data)> reqs, Action<List<DeviceCanFrame>> send, CancellationToken ct)
    {
        var replies = new Dictionary<int, byte[]>();
        foreach (var window in reqs.Chunk(Window))
        {
            var todo = window.ToList();
            for (var attempt = 0; todo.Count > 0; attempt++)
            {
                if (attempt > Retries)
                    return null;

                var waits = todo.Select(r =>
                {
                    var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pending[((int)cmd, r.Offset)] = tcs;
                    return (Req: r, Tcs: tcs);
                }).ToList();

                send(todo.Select(r => ToFrame(baseId, cmd, r.Offset, r.Data)).ToList());
                await Task.WhenAny(Task.WhenAll(waits.Select(w => w.Tcs.Task)), Task.Delay(ReplyTimeout, ct));
                ct.ThrowIfCancellationRequested();

                todo.Clear();
                foreach (var (req, tcs) in waits)
                {
                    if (tcs.Task.IsCompletedSuccessfully)
                    {
                        replies[req.Offset] = tcs.Task.Result;
                        Done += req.Data.Length > 0 ? req.Data.Length : ChunkSize;
                    }
                    else
                        todo.Add(req);
                }
            }
        }
        return replies;
    }

    private static DeviceCanFrame ToFrame(int baseId, MessageCommand cmd, int offset, byte[] data)
    {
        var payload = new byte[8]; // The firmware only accepts DLC 8
        payload[0] = (byte)cmd;
        payload[1] = (byte)(offset >> 8);
        payload[2] = (byte)offset;
        data.CopyTo(payload, 3);
        return new DeviceCanFrame
        {
            SendOnly = true, // Replies are matched here, not in the DeviceManager queue
            DeviceBaseId = baseId,
            Frame = new CanFrame(Id: baseId + FwDevice.ConfigTxOffset, Len: 8, Payload: payload),
            Name = cmd.ToString()
        };
    }
}
