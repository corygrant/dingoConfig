using application.Services;
using domain.Interfaces;
using domain.Enums;
using infrastructure.Adapters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace api.Services;

public class DemoInitializationService : IHostedService
{
    private readonly DemoModeService _demoMode;
    private readonly DeviceManager _deviceManager;
    private readonly ICommsAdapterManager _adapterManager;
    private readonly SimPlayback _simPlayback;
    private readonly SimAdapter _simAdapter;
    private readonly ILogger<DemoInitializationService> _logger;

    public DemoInitializationService(
        DemoModeService demoMode,
        DeviceManager deviceManager,
        ICommsAdapterManager adapterManager,
        SimPlayback simPlayback,
        SimAdapter simAdapter,
        ILogger<DemoInitializationService> logger)
    {
        _demoMode = demoMode;
        _deviceManager = deviceManager;
        _adapterManager = adapterManager;
        _simPlayback = simPlayback;
        _simAdapter = simAdapter;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_demoMode.IsEnabled)
        {
            _logger.LogInformation("Demo mode disabled, skipping initialization");
            return;
        }

        _logger.LogInformation("Demo mode enabled, initializing demo environment");

        // Pre-populate devices
        var devices = _demoMode.GetPrePopulatedDevices();
        foreach (var device in devices)
        {
            _logger.LogInformation("Adding demo device: {Type} - {Name} (BaseId: {BaseId})",
                device.Type, device.Name, device.BaseId);
            _deviceManager.AddDevice(device.Type, device.Name, device.BaseId);
        }

        if (_demoMode.AutoStart)
        {
            // Wait a bit for services to fully initialize
            await Task.Delay(1000, cancellationToken);

            // Load sample CAN log file
            var logFilePath = Path.Combine(AppContext.BaseDirectory, _demoMode.SampleLogFile);
            if (File.Exists(logFilePath))
            {
                _logger.LogInformation("Loading demo CAN log file: {Path}", logFilePath);
                var (success, error) = await _simPlayback.LoadFile(logFilePath);
                if (!success)
                {
                    _logger.LogWarning("Failed to load demo CAN log: {Error}", error);
                    return;
                }
            }
            else
            {
                _logger.LogWarning("Demo CAN log file not found: {Path}", logFilePath);
                return;
            }

            // Connect to Sim adapter
            _logger.LogInformation("Connecting to Sim adapter");
            var connectSuccess = await _adapterManager.ConnectAsync(
                _simAdapter,
                "Simulated",
                CanBitRate.BitRate1000K,
                cancellationToken);

            if (!connectSuccess)
            {
                _logger.LogWarning("Failed to connect Sim adapter");
                return;
            }

            // Start playback with looping
            _logger.LogInformation("Starting CAN log playback (looping enabled)");
            _simPlayback.Loop = true;
            await _simPlayback.Play();
        }

        _logger.LogInformation("Demo initialization complete");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
