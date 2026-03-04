using System.Diagnostics;
using System.IO.Ports;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

// ReSharper disable MemberCanBePrivate.Global

namespace infrastructure.Adapters;

public abstract class SerialAdapter : ICommsAdapter
{
    public abstract string Name { get; }

    protected SerialPort? Serial;
    protected string? PortName;
    protected Stopwatch? RxStopwatch;
    protected Timer? ConnectionMonitorTimer;
    protected TimeSpan RxTimeDelta { get; set; }

    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public bool IsConnected => RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public event DataReceivedHandler? DataReceived;
    public event EventHandler? Disconnected;

    public virtual Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        try
        {
            PortName = port;
            Serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            Serial.Handshake = Handshake.None;
            Serial.NewLine = "\r";
            Serial.ReadBufferSize = 65536;
            Serial.ReadTimeout = 500;
            Serial.ErrorReceived += _serial_ErrorReceived;
            Serial.Open();

            RxStopwatch = Stopwatch.StartNew();
        }
        catch
        {
            Serial?.ErrorReceived -= _serial_ErrorReceived;
            Serial?.Close();

            RxStopwatch?.Stop();
            PortName = null;

            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public virtual Task<bool> StartAsync(CancellationToken ct)
    {
        if (Serial is { IsOpen: false }) return Task.FromResult(false);

        StartConnectionMonitor();
        StartReadLoop();

        return Task.FromResult(true);
    }

    public virtual Task<bool> StopAsync()
    {
        StopReadLoop();
        StopConnectionMonitor();

        Serial?.Close();

        RxStopwatch?.Stop();

        //Set time delta to a high value to set IsConnected to false
        RxTimeDelta = new TimeSpan(1, 0, 0);

        return Task.FromResult(true);
    }

    public virtual Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (Serial is { IsOpen: false } || (frame.Payload.Length <= 0))
            return Task.FromResult(false);

        try
        {
            Serial?.Write(frame.Payload, 0, frame.Len);
        }
        catch (InvalidOperationException)
        {
            HandleDisconnection();
            return Task.FromResult(false);
        }
        catch (IOException)
        {
            HandleDisconnection();
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            HandleDisconnection();
            return Task.FromResult(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    protected void StartReadLoop()
    {
        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoop(_readCts.Token));
    }

    protected void StopReadLoop()
    {
        _readCts?.Cancel();
        try { _readTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;
    }

    private void ReadLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && (Serial?.IsOpen ?? false))
        {
            try
            {
                var raw = Serial.ReadLine(); // Blocks until '\r'; Serial.NewLine = "\r"
                ProcessFrame(raw);
            }
            catch (TimeoutException) { continue; }
            catch (InvalidOperationException) { HandleDisconnection(); return; }
            catch (IOException) { HandleDisconnection(); return; }
            catch (ArgumentOutOfRangeException) { HandleDisconnection(); return; }
        }
    }

    // '0'-'9' → 0-9, 'A'-'F'/'a'-'f' → 10-15; no string allocation
    private static int HexCharToInt(char c) =>
        c <= '9' ? c - '0' : (c & 0x1F) + 9;

    private void ProcessFrame(string raw)
    {
        if (raw.Length < 5) return; //'t' msg is always at least 5 bytes long (t + ID ID ID + DLC)
        if (raw[0] != 't') return; // Skip non-message frames (acknowledgments, status, etc.)

        try
        {
            if (RxStopwatch != null)
            {
                RxTimeDelta = new TimeSpan(RxStopwatch.ElapsedMilliseconds);
                RxStopwatch.Restart();
            }

            var id = (HexCharToInt(raw[1]) << 8) | (HexCharToInt(raw[2]) << 4) | HexCharToInt(raw[3]);
            var len = HexCharToInt(raw[4]);

            byte[] payload;
            if (len > 0 && raw.Length >= 5 + len * 2)
            {
                payload = new byte[len];
                for (var i = 0; i < len; i++)
                {
                    var high = HexCharToInt(raw[5 + i * 2]);
                    var low  = HexCharToInt(raw[6 + i * 2]);
                    payload[i] = (byte)((high << 4) | low);
                }
            }
            else
            {
                payload = new byte[8];
            }

            var frame = new CanFrame(id, len, payload);
            DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
        }
        catch (IndexOutOfRangeException)
        {
            // Skip malformed frames
        }
    }

    private void _serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        HandleDisconnection();
    }

    protected void StartConnectionMonitor()
    {
        // Start connection monitoring (check every 500ms)
        ConnectionMonitorTimer = new Timer(MonitorConnection, null,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500));
    }

    protected void StopConnectionMonitor()
    {
        ConnectionMonitorTimer?.Dispose();
        ConnectionMonitorTimer = null;
    }

    private void MonitorConnection(object? state)
    {
        if (!IsConnected) return;

        try
        {
            // Cross-platform approach: Check if the port still exists in the system
            // Works on Linux, Windows, and macOS
            if (string.IsNullOrEmpty(PortName))
            {
                HandleDisconnection();
                return;
            }

            var availablePorts = SerialPort.GetPortNames();
            if (!availablePorts.Contains(PortName))
            {
                // Port no longer exists - device was unplugged
                HandleDisconnection();
            }
        }
        catch (Exception)
        {
            // Exception during port enumeration
            HandleDisconnection();
        }
    }

    protected void HandleDisconnection()
    {
        //Note: Disconnecting is handled by the CommsAdapterManager
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}
