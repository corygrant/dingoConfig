// infrastructure/BackgroundServices/CanDataPipeline.cs

using System.Threading.Channels;
using api.Services;
using api.Models;
using api.Adapters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace api.BackgroundServices;

public class CommsDataPipeline(
    ICommsAdapterManager adapterManager,
    DeviceManager deviceManager,
    ILogger<CommsDataPipeline> logger)
    : BackgroundService
{
    // RX Channel - Incoming CAN frames from adapter
    private readonly Channel<CanData> _rxChannel = Channel.CreateBounded<CanData>(
        new BoundedChannelOptions(50000)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Don't block adapter
            SingleReader = true,
            SingleWriter = false // Multiple adapters could write
        });
    
    // TX Channel - Outgoing CAN frames to adapter
    private readonly Channel<CanData> _txChannel = Channel.CreateBounded<CanData>(
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
        // Subscribe to adapter events
        //if (adapterManager.ActiveAdapter == null) throw new NullReferenceException();
            
        //adapterManager.ActiveAdapter.DataReceived += OnDataReceived;
        
        // Start both pipelines
        var rxTask = ProcessRxPipelineAsync(stoppingToken);
        var txTask = ProcessTxPipelineAsync(stoppingToken);
        
        await Task.WhenAll(rxTask, txTask);
    }
    
    // ============================================
    // RX Pipeline (Receive from CAN bus)
    // ============================================
    
    private void OnDataReceived(object? sender, CanDataEventArgs e)
    {
        // Queue frame for processing (non-blocking)
        _rxChannel.Writer.TryWrite(e.Data);
    }
    
    private async Task ProcessRxPipelineAsync(CancellationToken ct)
    {
        logger.LogInformation("RX Pipeline started");
        
        await foreach (var frame in _rxChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                // Route frame to appropriate device
                // Devices will parse it and update their state
                // (Devices subscribed to adapter events handle this)
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
            while (!ct.IsCancellationRequested)
            {
                if (_txChannel.Reader.TryRead(out var normalRequest))
                {
                    await TransmitFrameAsync(normalRequest, ct);
                    continue;
                }
                
                // If no messages, wait a bit
                await Task.Delay(1, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        
        logger.LogInformation("TX Pipeline stopped");
    }
    
    private async Task TransmitFrameAsync(CanData data, CancellationToken ct)
    {
        try
        {
            if (adapterManager.ActiveAdapter == null) throw new NullReferenceException();
            
            adapterManager.ActiveAdapter.WriteAsync(data, ct);
            
            logger.LogDebug(
                "TX frame sent: CanId={Id:X}, Length={Len}", 
                data.Id, 
                data.Len);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "Error transmitting frame: {CanId:X}", 
                data.Id);
        }
    }
    
    // ============================================
    // Public API for Transmitting
    // ============================================
    
    /// <summary>
    /// Queue a frame for transmission (normal priority)
    /// </summary>
    public void QueueTransmit(CanData frame)
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