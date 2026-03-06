using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

// ReSharper disable MemberCanBePrivate.Global

namespace infrastructure.Adapters;

public class SlcanAdapter : ICommsAdapter
{
    public virtual string Name => "SLCAN";

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
    
    private int _bitrate;

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        try
        {
            PortName = port;
            Serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            Serial.Handshake = Handshake.None;
            Serial.NewLine = "\r";
            Serial.ReadBufferSize = 65536;
            Serial.WriteBufferSize = 65536;
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
        
        _bitrate = ToSlcanBitrate(bitRate);
        
        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (Serial is { IsOpen: false }) return Task.FromResult(false);

        try
        {
            // Send SLCAN commands
            var sData = "C\r";
            if (Serial != null)
            {
                Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

                //Set bitrate
                sData = "S" + _bitrate + "\r";
                Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

                //Open slcan
                sData = "O\r";
                Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));
            }

            StartConnectionMonitor();
            StartReadLoop();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        StopReadLoop();
        StopConnectionMonitor();

        if (Serial is { IsOpen: false }) return Task.FromResult(false);

        const string sData = "C\r";
        if (Serial == null) return Task.FromResult(true);

        try
        {
            Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));
        }
        catch
        {
            // Ignore errors during shutdown
        }

        RxStopwatch?.Stop();

        //Set time delta to a high value to set IsConnected to false
        RxTimeDelta = new TimeSpan(1, 0, 0);

        return Task.FromResult(true);
    }

    // Encodes a single CAN frame into buffer at offset using SLCAN format.
    // Returns the number of bytes written.
    private static int EncodeFrame(CanFrame frame, byte[] buffer, int offset)
    {
        buffer[offset]     = (byte)'t';
        buffer[offset + 1] = (byte)((frame.Id & 0xF00) >> 8);
        buffer[offset + 2] = (byte)((frame.Id & 0xF0) >> 4);
        buffer[offset + 3] = (byte)(frame.Id & 0xF);
        buffer[offset + 4] = (byte)frame.Len;

        var lastByte = 0;
        for (var i = 0; i < frame.Len; i++)
        {
            buffer[offset + 5 + (i * 2)] = (byte)((frame.Payload[i] & 0xF0) >> 4);
            buffer[offset + 6 + (i * 2)] = (byte)(frame.Payload[i] & 0xF);
            lastByte = 6 + (i * 2);
        }

        buffer[offset + lastByte + 1] = (byte)'\r';

        for (var i = 1; i < lastByte + 1; i++)
            buffer[offset + i] += buffer[offset + i] < 0xA ? (byte)0x30 : (byte)0x37;

        return lastByte + 2;
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (Serial is { IsOpen: false } || frame.Payload.Length != 8)
            return Task.FromResult(false);

        try
        {
            var buffer = new byte[22];
            var len = EncodeFrame(frame, buffer, 0);
            Serial?.Write(buffer, 0, len);
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

    public Task<bool> WriteBatchAsync(IReadOnlyList<CanFrame> frames, CancellationToken ct)
    {
        if (Serial is { IsOpen: false }) return Task.FromResult(false);

        var frameBuffer = new byte[22];
        try
        {
            foreach (var frame in frames)
            {
                if (frame.Payload.Length != 8) continue;
                var len = EncodeFrame(frame, frameBuffer, 0);
                Serial!.Write(frameBuffer, 0, len);
            }
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
        try { _readTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch
        {
            // ignored
        }

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
            catch (TimeoutException) { }
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

    private int ToSlcanBitrate(CanBitRate canBitRate)
    {
        return canBitRate switch
        {
            CanBitRate.BitRate125K => 4,
            CanBitRate.BitRate250K => 5,
            CanBitRate.BitRate500K => 6,
            CanBitRate.BitRate1000K => 8,
            //Default to 500k
            _ => 6
        };
    }
}