using System.Diagnostics;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Peak.Can.Basic;
using MessageType = Peak.Can.Basic.MessageType;

namespace infrastructure.Adapters;

public class PcanAdapter  : ICommsAdapter
{
    public string Name => "PCAN";

    private Worker? _worker;
    private PcanChannel? _channel;
    private Timer? _statusMonitorTimer;
    private bool _wasConnected;

    public event DataReceivedHandler? DataReceived;
    public event EventHandler? Disconnected;

    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }
    public bool IsConnected => RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        var channel = PcanChannel.Usb01;
        _channel = channel; // Store channel for disconnection detection
        _worker = new Worker(channel, ConvertBaudRate(bitRate));
        _rxStopwatch = Stopwatch.StartNew();
        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (_worker == null) return Task.FromResult(false);

        _worker.MessageAvailable += OnMessageAvailable;
        try
        {
            _worker.Start(true);
            _wasConnected = true;

            // Start status monitoring timer (check every 500ms)
            // Primary detection happens in read/write operations
            _statusMonitorTimer = new Timer(MonitorConnectionStatus, null,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(500));
        }
        catch (PcanBasicException e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        if (_worker == null) return Task.FromResult(false);

        _statusMonitorTimer?.Dispose();
        _statusMonitorTimer = null;

        _worker.MessageAvailable -= OnMessageAvailable;
        _worker.Stop();

        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_worker == null || frame.Payload.Length != 8 || !(_worker.Active)) return Task.FromResult(false);

        try
        {
            var msg = new PcanMessage((uint)frame.Id, MessageType.Standard, (byte)frame.Len, frame.Payload);
            var result = _worker.Transmit(msg, out _);
            
            if (!result)
            {
                // Transmit failed - might indicate disconnection
                // Check if worker is still active
                if (!_worker.Active)
                {
                    HandleDisconnection();
                }
                return Task.FromResult(false);
            }

            return Task.FromResult(result);
        }
        catch (PcanBasicException)
        {
            // PCAN exception during write - device is disconnected
            HandleDisconnection();
            return Task.FromResult(false);
        }
        catch
        {
            // Other exception
            return Task.FromResult(false);
        }
    }

    private void OnMessageAvailable(object? sender, MessageAvailableEventArgs e)
    {
        if (_worker == null) return;

        try
        {
            if (!_worker.Dequeue(e.QueueIndex, out var msg, out _)) return;

            if (_rxStopwatch != null)
            {
                RxTimeDelta = new TimeSpan(_rxStopwatch.ElapsedMilliseconds);
                _rxStopwatch.Restart();
            }

            if (msg.DLC <= 0) return;

            // Only copy the actual number of bytes specified by DLC, not the full 8-byte buffer
            // The length of the payload array is checked by receiving devices
            var payload = new byte[msg.DLC];
            Array.Copy(msg.Data, payload, msg.DLC);

            var frame = new CanFrame
            (
                Id: Convert.ToInt16(msg.ID),
                Len: Convert.ToInt16(msg.DLC),
                Payload: payload
            );
            DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
        }
        catch (PcanBasicException)
        {
            // PCAN exception during dequeue - device may be disconnected
            HandleDisconnection();
        }
        catch
        {
            // Ignore other exceptions in message processing
        }
    }

    private void MonitorConnectionStatus(object? state)
    {
        if (_worker == null || _channel == null)
        {
            HandleDisconnection();
            return;
        }

        bool isCurrentlyConnected = true;

        try
        {
            // Cross-platform device existence check (similar to USB/SLCAN adapters)
            if (OperatingSystem.IsLinux())
            {
                // On Linux, check if the PCAN USB device file still exists
                // When USB is unplugged, this file disappears
                if (!File.Exists("/dev/pcanusb32"))
                {
                    isCurrentlyConnected = false;
                }
            }
            else
            {
                // On Windows/macOS, check Worker.Active status
                var active = _worker.Active;
                if (!active)
                {
                    isCurrentlyConnected = false;
                }
            }
        }
        catch (PcanBasicException)
        {
            // PCAN exception means device issue
            isCurrentlyConnected = false;
        }
        catch
        {
            // Any other exception also indicates disconnection
            isCurrentlyConnected = false;
        }

        // Detect transition from connected to disconnected
        if (_wasConnected && !isCurrentlyConnected)
        {
            HandleDisconnection();
        }

        _wasConnected = isCurrentlyConnected;
    }

    private void HandleDisconnection()
    {
        if (!_wasConnected) return; // Already handled

        _wasConnected = false;
        _statusMonitorTimer?.Dispose();
        _statusMonitorTimer = null;

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private static Bitrate ConvertBaudRate(CanBitRate baud)
    {
        return baud switch
        {
            CanBitRate.BitRate1000K => Bitrate.Pcan1000,
            CanBitRate.BitRate500K => Bitrate.Pcan500,
            CanBitRate.BitRate250K => Bitrate.Pcan250,
            CanBitRate.BitRate125K => Bitrate.Pcan125,
            _ => Bitrate.Pcan500
        };
    }
}