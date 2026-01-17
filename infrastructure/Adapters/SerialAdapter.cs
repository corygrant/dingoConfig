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
    
    public bool IsConnected => RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public event DataReceivedHandler? DataReceived;
    public event EventHandler? Disconnected;

    public virtual Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        try
        {
            PortName = port; // Store port name for disconnection detection
            Serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            Serial.Handshake = Handshake.None;
            Serial.NewLine = "\r";
            Serial.DataReceived += _serial_DataReceived;
            Serial.ErrorReceived += _serial_ErrorReceived;
            Serial.Open();

            RxStopwatch = Stopwatch.StartNew();
        }
        catch
        {
            Serial?.DataReceived -= _serial_DataReceived;
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

        return Task.FromResult(true);
    }
    
    public virtual Task<bool> StopAsync()
    {
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
            var data = new byte[frame.Len];
            for(var i = 0; i < data.Length; i++)
            {
                data[i] = frame.Payload[i];
            }

            Serial?.Write(data, 0, data.Length);
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

    protected virtual void _serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var ser = (SerialPort)sender;
        if (!ser.IsOpen)
        {
            HandleDisconnection();
            return;
        }

        try
        {
            var data = ser.ReadExisting();
            foreach (var raw in data.Split('\r'))
            {
                if (raw.Length < 5) continue; //'t' msg is always at least 5 bytes long (t + ID ID ID + DLC)
                if (raw[..1] != "t") continue; // Skip non-message frames (e.g., acknowledgments, status)

                try
                {
                    if (RxStopwatch != null)
                    {
                        RxTimeDelta = new TimeSpan(RxStopwatch.ElapsedMilliseconds);
                        RxStopwatch.Restart();
                    }

                    var id = int.Parse(raw.Substring(1, 3), System.Globalization.NumberStyles.HexNumber);
                    var len = int.Parse(raw.Substring(4, 1), System.Globalization.NumberStyles.HexNumber);

                    //Msg comes in as a hex string
                    //For example, an ID of 2008(0x7D8) will be sent as "t7D8...."
                    //The string needs to be parsed into an int using int.Parse
                    //The payload bytes are split across 2 bytes (a nibble each)
                    //For example, a payload byte of 28 (0001 1100) would be split into "1C"
                    byte[] payload;
                    if ((len > 0) && (raw.Length >= 5 + len * 2))
                    {
                        payload = new byte[len];
                        for (var i = 0; i < payload.Length; i++)
                        {
                            var highNibble = int.Parse(raw.Substring(i * 2 + 5, 1), System.Globalization.NumberStyles.HexNumber);
                            var lowNibble = int.Parse(raw.Substring(i * 2 + 6, 1), System.Globalization.NumberStyles.HexNumber);
                            payload[i] = (byte)(((highNibble & 0x0F) << 4) + (lowNibble & 0x0F));
                        }
                    }
                    else
                    {
                        //Length was 0, create empty data
                        payload = new byte[8];
                    }

                    var frame = new CanFrame(id, len, payload);

                    DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
                }
                catch (FormatException ex)
                {
                    // Skip malformed frames - log for debugging if needed
                    Console.WriteLine($"SlcanAdapter: Malformed frame skipped: '{raw}' - {ex.Message}");
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    // Skip frames with invalid indices
                    Console.WriteLine($"SlcanAdapter: Invalid frame format: '{raw}' - {ex.Message}");
                }
            } // end foreach
        }
        catch (InvalidOperationException)
        {
            // Port has been closed or device unplugged
            HandleDisconnection();
        }
        catch (IOException)
        {
            // I/O error - device likely unplugged
            HandleDisconnection();
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied - port may have been disconnected
            HandleDisconnection();
        }
    }

    private void _serial_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        // Serial port error detected - likely disconnection
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
