using System.Text;
using domain.Enums;
using domain.Models;

namespace infrastructure.Adapters;

public class SlcanAdapter : SerialAdapter
{
    public override string Name => "SLCAN";

    private CanBitRate _bitrate;

    public override Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        _bitrate = bitRate;
        return base.InitAsync(port, bitRate, ct);
    }

    public override Task<bool> StartAsync(CancellationToken ct)
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
                sData = "S" + (int)_bitrate + "\r";
                Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));

                //Open slcan
                sData = "O\r";
                Serial.Write(Encoding.ASCII.GetBytes(sData), 0, Encoding.ASCII.GetByteCount(sData));
            }

            StartConnectionMonitor();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public override Task<bool> StopAsync()
    {
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

        Serial.Close();

        return Task.FromResult(true);
    }

    public override Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (Serial is { IsOpen: false } || frame.Payload.Length != 8)
            return Task.FromResult(false);

        try
        {
            // Format message in SLCAN protocol
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

            Serial?.Write(d, 0, lastByte + 2);
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
}