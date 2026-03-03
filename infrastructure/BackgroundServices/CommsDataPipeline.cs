// infrastructure/BackgroundServices/CanDataPipeline.cs

using System.Threading.Channels;
using application.Models;
using application.Services;
using domain.Models;
using domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace infrastructure.BackgroundServices;

public class CommsDataPipeline(
    ICommsAdapterManager adapterManager,
    DeviceManager deviceManager,
    CanMsgLogger msgLogger,
    ILogger<CommsDataPipeline> logger)
    : BackgroundService
{
    // RX Channel - Incoming CAN frames from adapter
    private readonly Channel<CanFrame> _rxChannel = Channel.CreateBounded<CanFrame>(
        new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Don't block adapter
            SingleReader = true,
            SingleWriter = false // Multiple adapters could write
        });
    
    // TX Channel - Outgoing CAN frames to adapter
    private readonly Channel<DeviceCanFrame> _txChannel = Channel.CreateBounded<DeviceCanFrame>(
        new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    // RX Channel - Large buffer for high message rate
    // Don't block adapter
    // Multiple adapters could write
    // TX Normal Priority Channel

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        adapterManager.Connected += OnConnect;
        
        // Set up the transmit callback for DeviceManager
        deviceManager.SetTransmitCallback(QueueTransmit);

        // Start both pipelines
        var rxTask = ProcessRxPipelineAsync(stoppingToken);
        var txTask = ProcessTxPipelineAsync(stoppingToken);

        await Task.WhenAll(rxTask, txTask);
    }

    private void OnConnect(object? sender, EventArgs e)
    {
        // Subscribe to adapter events
        if (adapterManager.ActiveAdapter == null) return;

        adapterManager.ActiveAdapter.DataReceived += OnDataReceived;
    }
    
    // ============================================
    // RX Pipeline (Receive from CAN bus)
    // ============================================
    
    private void OnDataReceived(object? sender, CanFrameEventArgs e)
    {
        // Queue frame for processing (non-blocking)
        _rxChannel.Writer.TryWrite(e.Frame);
    }
    
    private async Task ProcessRxPipelineAsync(CancellationToken ct)
    {
        logger.LogInformation("RX Pipeline started");

        await foreach (var frame in _rxChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                msgLogger.Log(DataDirection.Rx, frame);
                
                // Route frame to DeviceManager, passes to all devices so they can update their state/config
                deviceManager.OnCanDataReceived(frame);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing RX frame: {CanId:X}", frame.Id);
            }
        }

        logger.LogInformation("RX Pipeline stopped");
    }
    
    // ============================================
    // TX Pipeline (Transmit to CAN bus)
    // ============================================
    
    private async Task ProcessTxPipelineAsync(CancellationToken ct)
    {
        logger.LogInformation("TX Pipeline started");
        
        try
        {
            while (await _txChannel.Reader.WaitToReadAsync(ct))
            {
                while (_txChannel.Reader.TryRead(out var deviceFrame))
                {
                    await TransmitFrameAsync(deviceFrame, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        
        logger.LogInformation("TX Pipeline stopped");
    }
    
    private async Task TransmitFrameAsync(DeviceCanFrame deviceFrame, CancellationToken ct)
    {
        var frame = deviceFrame.Frame;
        try
        {
            if (adapterManager.ActiveAdapter == null)
                return;

            var success = await adapterManager.ActiveAdapter.WriteAsync(frame, ct);
            if (!success) 
                return;

            msgLogger.Log(DataDirection.Tx, frame);

            // Start timeout timer now that frame has been physically transmitted
            deviceManager.OnFrameTransmitted(deviceFrame);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error transmitting frame: {CanId:X}",
                frame.Id);
        }
    }
    
    // ============================================
    // Public API for Transmitting
    // ============================================
    
    /// <summary>
    /// Queue a frame for transmission (normal priority)
    /// </summary>
    private void QueueTransmit(DeviceCanFrame frame)
    {
        _txChannel.Writer.TryWrite(frame);
    }
    
    public override void Dispose()
    {
        if (adapterManager is { ActiveAdapter: not null })
        {
            adapterManager.ActiveAdapter.DataReceived -= OnDataReceived;
        }

        base.Dispose();
    }
}