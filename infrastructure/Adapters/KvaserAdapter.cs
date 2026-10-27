using System.Diagnostics;
using System.Runtime.InteropServices;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

// Kvaser CAN interface via CANlib (canlib32.dll, installed with the Kvaser drivers - Windows only;
// on Linux Kvaser devices use SocketCAN). Port is the CANlib channel index, blank/non-numeric = 0.
public class KvaserAdapter : ICommsAdapter
{
    public string Name => "Kvaser";

    private const int InterFrameDelayMs = 2;

    private int _handle = -1;
    private Thread? _readThread;
    private CancellationTokenSource? _readCts;
    private readonly Lock _writeLock = new();

    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }
    public bool IsConnected => _handle >= 0 && RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public event DataReceivedHandler? DataReceived;
    public event EventHandler? Disconnected;

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        var channel = int.TryParse(port, out var c) ? c : 0;
        try
        {
            canInitializeLibrary();
            _handle = canOpenChannel(channel, 0);
            if (_handle < 0)
            {
                _handle = -1;
                return Task.FromResult(false);
            }

            // Predefined bitrate constants are passed as freq; tseg/sjw args are ignored
            if (canSetBusParams(_handle, new CLong(BitrateConst(bitRate)), 0, 0, 0, 0, 0) != canOK ||
                canSetBusOutputControl(_handle, canDRIVER_NORMAL) != canOK)
            {
                canClose(_handle);
                _handle = -1;
                return Task.FromResult(false);
            }

            _rxStopwatch = Stopwatch.StartNew();
            return Task.FromResult(true);
        }
        catch (DllNotFoundException)
        {
            // Kvaser drivers not installed
            _handle = -1;
            return Task.FromResult(false);
        }
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (_handle < 0 || canBusOn(_handle) != canOK) return Task.FromResult(false);

        _readCts = new CancellationTokenSource();
        var token = _readCts.Token;
        _readThread = new Thread(() => ReadLoop(token)) { IsBackground = true, Name = "KvaserRead" };
        _readThread.Start();
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        _readCts?.Cancel();
        _readThread?.Join(TimeSpan.FromSeconds(2));
        _readCts?.Dispose();
        _readCts = null;
        _readThread = null;

        if (_handle >= 0)
        {
            try
            {
                canBusOff(_handle);
                canClose(_handle);
            }
            catch
            {
                // Ignore errors during shutdown
            }

            _handle = -1;
        }

        RxTimeDelta = TimeSpan.FromHours(1);
        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_handle < 0) return Task.FromResult(false);

        var data = new byte[8];
        Array.Copy(frame.Payload, data, Math.Min(frame.Len, frame.Payload.Length));

        for (var i = 0; i < 3; i++)
        {
            int status;
            lock (_writeLock) status = canWrite(_handle, new CLong(frame.Id), data, (uint)frame.Len, canMSG_STD);

            if (status == canOK) return Task.FromResult(true);
            if (status != canERR_TXBUFOFL)
            {
                HandleDisconnection();
                return Task.FromResult(false);
            }

            Thread.Sleep(1);
        }

        return Task.FromResult(false);
    }

    public Task<bool> WriteBatchAsync(IReadOnlyList<CanFrame> frames, CancellationToken ct)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            if (ct.IsCancellationRequested || !WriteAsync(frames[i], ct).Result)
                return Task.FromResult(false);

            if (i < frames.Count - 1)
                Thread.Sleep(InterFrameDelayMs);
        }

        return Task.FromResult(true);
    }

    private void ReadLoop(CancellationToken ct)
    {
        var data = new byte[8];
        var consecutiveErrors = 0;
        while (!ct.IsCancellationRequested && _handle >= 0)
        {
            var status = canReadWait(_handle, out var id, data, out var dlc, out var flag, out _, new CULong(100));
            if (status == canERR_NOMSG)
            {
                consecutiveErrors = 0;
                continue;
            }

            if (status != canOK)
            {
                // Tolerate transients, a burst of errors means unplugged/bus-off
                if (++consecutiveErrors > 20)
                {
                    HandleDisconnection();
                    return;
                }
                continue;
            }

            consecutiveErrors = 0;

            if (_rxStopwatch != null)
            {
                RxTimeDelta = new TimeSpan(_rxStopwatch.ElapsedMilliseconds);
                _rxStopwatch.Restart();
            }

            // Only standard 11-bit data frames
            if ((flag & (canMSG_EXT | canMSG_ERROR_FRAME | canMSG_RTR)) != 0 || dlc == 0) continue;

            var len = (int)Math.Min(dlc, 8u);
            var payload = new byte[len];
            Array.Copy(data, payload, len);

            var frame = new CanFrame((int)(id.Value & 0x7FF), len, payload);
            DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
        }
    }

    private void HandleDisconnection()
    {
        //Note: Disconnecting is handled by the CommsAdapterManager
        if (_handle < 0) return;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private static int BitrateConst(CanBitRate bitRate) => bitRate switch
    {
        CanBitRate.BitRate1000K => -1, // canBITRATE_1M
        CanBitRate.BitRate500K => -2,  // canBITRATE_500K
        CanBitRate.BitRate250K => -3,  // canBITRATE_250K
        CanBitRate.BitRate125K => -4,  // canBITRATE_125K
        CanBitRate.BitRate100K => -5,  // canBITRATE_100K
        _ => -2
    };

    // CANlib P/Invoke. C 'long' is 32-bit on Windows, hence CLong/CULong
    private const int canOK = 0;
    private const int canERR_NOMSG = -2;
    private const int canERR_TXBUFOFL = -13;
    private const uint canMSG_RTR = 0x0001;
    private const uint canMSG_STD = 0x0002;
    private const uint canMSG_EXT = 0x0004;
    private const uint canMSG_ERROR_FRAME = 0x0020;
    private const uint canDRIVER_NORMAL = 4;

    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern void canInitializeLibrary();
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canOpenChannel(int channel, int flags);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canSetBusParams(int hnd, CLong freq, uint tseg1, uint tseg2, uint sjw, uint noSamp, uint syncmode);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canSetBusOutputControl(int hnd, uint drivertype);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canBusOn(int hnd);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canBusOff(int hnd);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canClose(int hnd);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canWrite(int hnd, CLong id, byte[] msg, uint dlc, uint flag);
    [DllImport("canlib32", CallingConvention = CallingConvention.StdCall)]
    private static extern int canReadWait(int hnd, out CLong id, byte[] msg, out uint dlc, out uint flag, out CULong time, CULong timeout);
}
