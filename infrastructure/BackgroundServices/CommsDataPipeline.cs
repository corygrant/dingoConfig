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
    
    // TX Channel - Outgoing CAN frame batches to adapter
    private readonly Channel<List<DeviceCanFrame>> _txChannel = Channel.CreateBounded<List<DeviceCanFrame>>(
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
        
        // Set up transmit callbacks for DeviceManager
        deviceManager.SetBatchTransmitCallback(batch => _txChannel.Writer.TryWrite(batch));

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

        try
        {
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
        }
        catch (OperationCanceledException)
        {
            // Expected during host shutdown/cancellation
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
                while (_txChannel.Reader.TryRead(out var batch))
                    await TransmitBatchAsync(batch, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        
        logger.LogInformation("TX Pipeline stopped");
    }
    
    private async Task TransmitBatchAsync(List<DeviceCanFrame> batch, CancellationToken ct)
    {
        if (adapterManager.ActiveAdapter == null) return;

        try
        {
            //Chunk writes to allow other devices to transmit
            foreach (var chunk in batch.Chunk(50))
            {
                var success = await adapterManager.ActiveAdapter.WriteBatchAsync(
                    chunk.Select(df => df.Frame).ToList(), ct);
                if (!success) return;

                foreach (var deviceFrame in chunk)
                {
                    msgLogger.Log(DataDirection.Tx, deviceFrame.Frame);
                    deviceManager.OnFrameTransmitted(deviceFrame);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transmitting batch of {Count} frames", batch.Count);
        }
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