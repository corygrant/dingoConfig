
#if LINUX
#warning Building with LINUX define enabled
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using domain.Enums;
using domain.Models;
using domain.Interfaces;

namespace infrastructure.Adapters;

public unsafe class SocketCanAdapter : ICommsAdapter
{
    public string Name => "SocketCAN";

    private int _fd = -1;
    private Thread? _rxThread;
    private bool _running;

    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }

    private string _ifName = "can0";

    public event DataReceivedHandler? DataReceived;

    public bool IsConnected => _fd >= 0 && RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public Task<bool> InitAsync(string iface, CanBitRate bitRate, CancellationToken ct)
    {
        _ifName = iface;
        _rxStopwatch = Stopwatch.StartNew();
        return Task.FromResult(true);
    }
    
    public event EventHandler? Disconnected;

    public Task<bool> StartAsync(CancellationToken ct)
    {
        try
        {
            _fd = Libc.socket(Libc.AF_CAN, Libc.SOCK_RAW, Libc.CAN_RAW);
            if (_fd < 0)
                return Task.FromResult(false);

            int ifIndex = Libc.if_nametoindex(_ifName);
            if (ifIndex == 0)
                return Task.FromResult(false);

            var addr = new SockAddrCan
            {
                can_family = Libc.AF_CAN,
                can_ifindex = ifIndex
            };

            int bindResult = Libc.bind(_fd, ref addr, Marshal.SizeOf<SockAddrCan>());
            if (bindResult < 0)
                return Task.FromResult(false);

            _running = true;
            _rxThread = new Thread(ReceiveLoop) { IsBackground = true };
            _rxThread.Start();

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopAsync()
    {
        _running = false;

        if (_fd >= 0)
        {
            Libc.close(_fd);
            _fd = -1;
        }
        
        Disconnected?.Invoke(this, EventArgs.Empty);

        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_fd < 0)
            return Task.FromResult(false);

        CanFrameRaw raw = default;
        raw.can_id = (uint)frame.Id;
        raw.can_dlc = (byte)frame.Len;

        for (int i = 0; i < frame.Len; i++)
            raw.data[i] = frame.Payload[i];

        int size = sizeof(CanFrameRaw);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(raw, ptr, false);

        int written = Libc.write(_fd, ptr, size);

        Marshal.FreeHGlobal(ptr);

        return Task.FromResult(written == size);
    }

    private void ReceiveLoop()
    {
        int size = sizeof(CanFrameRaw);
        IntPtr ptr = Marshal.AllocHGlobal(size);

        while (_running && _fd >= 0)
        {
            int read = Libc.read(_fd, ptr, size);
            if (read == size)
            {
                var raw = Marshal.PtrToStructure<CanFrameRaw>(ptr)!;

                if (_rxStopwatch != null)
                {
                    RxTimeDelta = TimeSpan.FromMilliseconds(_rxStopwatch.ElapsedMilliseconds);
                    _rxStopwatch.Restart();
                }

                var payload = new byte[raw.can_dlc];
                for (int i = 0; i < raw.can_dlc; i++)
                    payload[i] = raw.data[i];

                var frame = new CanFrame((int)raw.can_id, raw.can_dlc, payload);
                DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
            }
        }

        Marshal.FreeHGlobal(ptr);
    }
}

// -----------------------------
// Native structs
// -----------------------------

[StructLayout(LayoutKind.Sequential)]
public unsafe struct CanFrameRaw
{
    public uint can_id;
    public byte can_dlc;
    public fixed byte __pad[3];   // required padding
    public fixed byte data[8];    // fixed buffer
}

[StructLayout(LayoutKind.Sequential)]
public struct SockAddrCan
{
    public ushort can_family;
    public int can_ifindex;
    public uint rx_id;
    public uint tx_id;
}

// -----------------------------
// Native Linux syscalls
// -----------------------------

public static class Libc
{
    public const int AF_CAN = 29;
    public const int SOCK_RAW = 3;
    public const int CAN_RAW = 1;

    [DllImport("libc", SetLastError = true)]
    public static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    public static extern int bind(int sockfd, ref SockAddrCan addr, int addrlen);

    [DllImport("libc", SetLastError = true)]
    public static extern int read(int fd, IntPtr buf, int count);

    [DllImport("libc", SetLastError = true)]
    public static extern int write(int fd, IntPtr buf, int count);

    [DllImport("libc", SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    public static extern int if_nametoindex(string ifname);
}
#endif
