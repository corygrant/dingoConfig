using System.Diagnostics;
using System.IO.Ports;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

public class UsbAdapter : ICommsAdapter
{
    public string? Name => "USB";
    private SerialPort? _serial;
    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }
    
    public event DataReceivedHandler? DataReceived;

    public bool IsConnected { get; private set; }

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        try
        {
            _serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            _serial.Handshake = Handshake.None;
            _serial.NewLine = "\r";
            _serial.DataReceived += _serial_DataReceived;
            _serial.Open();

            _rxStopwatch = Stopwatch.StartNew();
        }
        catch
        {
            _serial?.DataReceived -= _serial_DataReceived;
            _serial?.Close();

            _rxStopwatch?.Stop();
            
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (_serial is { IsOpen: false }) return Task.FromResult(false);
        
        IsConnected = true;
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        _serial?.DataReceived -= _serial_DataReceived;
        _serial?.Close();
        _rxStopwatch?.Stop();

        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_serial is { IsOpen: false } || (frame.Payload.Length <= 0)) 
            return Task.FromResult(false);

        try
        {
            var data = new byte[frame.Len];
            for(var i = 0; i < data.Length; i++)
            {
                data[i] = frame.Payload[i];
            }

            _serial?.Write(data, 0, data.Length);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
    
    private void _serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var ser = (SerialPort)sender;
        if (!ser.IsOpen) return;

        foreach (var raw in ser.ReadExisting().Split('\r'))
        {
            if (raw.Length < 5) continue; //'t' msg is always at least 5 bytes long (t + ID ID ID + DLC)
            if (raw[..1] != "t") continue; // Skip non-message frames (e.g., acknowledgments, status)

            try
            {
                if (_rxStopwatch != null)
                {
                    RxTimeDelta = new TimeSpan(_rxStopwatch.ElapsedMilliseconds);
                    _rxStopwatch.Restart();
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
        }
    }
}