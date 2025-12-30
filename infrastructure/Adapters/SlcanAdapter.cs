using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using domain.Enums;
using domain.Models;
using domain.Interfaces;

namespace infrastructure.Adapters;

public class SlcanAdapter : ICommsAdapter
{
    public string Name => "SLCAN";
    private SerialPort? _serial;
    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }

    private CanBitRate _bitrate;
    
    public event DataReceivedHandler? DataReceived;

    public bool IsConnected => RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        try
        {
            _bitrate = bitRate;
            _serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            _serial.Handshake = Handshake.None;
            _serial.NewLine = "\r";
            _serial.DataReceived += _serial_DataReceived;
            _serial.Open();

            _rxStopwatch = Stopwatch.StartNew();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (_serial is { IsOpen: false }) return Task.FromResult(false);
        
        try
        {
            var sData = "C\r";
            if (_serial != null)
            {
                _serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

                //Set bitrate
                sData = "S" + (int)_bitrate + "\r";
                _serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

                //Open slcan
                sData = "O\r";
                _serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));
            }
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
        if (_serial is { IsOpen: false }) return Task.FromResult(false);

        const string sData = "C\r";
        if (_serial == null) return Task.FromResult(true);
        
        _serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

        _serial.DataReceived -= _serial_DataReceived;

        _serial.Close();

        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_serial is { IsOpen: false } || frame.Payload.Length != 8) 
            return Task.FromResult(false);

        try
        {
            var d = new byte[22];
            d[0] = (byte)'t';
            d[1] = (byte)((frame.Id & 0xF00) >> 8);
            d[2] = (byte)((frame.Id & 0xF0) >> 4);
            d[3] = (byte)(frame.Id & 0xF);
            d[4] = (byte)frame.Len;

            var lastByte = 0;

            for (var i = 0; i < frame.Len; i++)
            {
                d[5 + (i * 2)] = Convert.ToByte((frame.Payload[i] & 0xF0) >> 4);
                d[6 + (i * 2)] = Convert.ToByte(frame.Payload[i] & 0xF);
                lastByte = 6 + (i * 2);
            }

            d[lastByte + 1] = Convert.ToByte('\r');

            for(var i = 1; i < lastByte + 1; i++)
            {
                if (d[i] < 0xA)
                    d[i] += 0x30;
                else
                    d[i] += 0x37;
            }

            _serial?.Write(d, 0, lastByte + 2);
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