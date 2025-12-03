using application.Services;
using AutoMapper;
using contracts.Devices;
using contracts.Devices.Pdm;
using contracts.Devices.Pdm.Functions;
using contracts.Devices.PdmMax;
using contracts.Devices.Canboard;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdmMax;
using domain.Devices.CanboardDevice;
using domain.Devices.dingoPdm.Functions;
using domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace infrastructure.BackgroundServices;

/// <summary>
/// Background service that broadcasts device state updates to DeviceStateService at 20 Hz.
/// Handles all device types (PDM, PDM-Max, CANBoard) and maps them to their respective DTOs.
/// </summary>
public class DeviceStateBroadcaster(
    DeviceManager deviceManager,
    DeviceStateService stateService,
    IMapper mapper,
    ILogger<DeviceStateBroadcaster> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Broadcast state at 20 Hz (every 50ms) as per CLAUDE.md
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        logger.LogInformation("DeviceStateBroadcaster started - broadcasting at 20 Hz");

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Get ALL devices regardless of type
                var allDevices = deviceManager.GetAllDevices();

                foreach (var device in allDevices)
                {
                    // Map each device type to its corresponding StateDto
                    var stateDto = MapDeviceToStateDto(device);

                    if (stateDto != null)
                    {
                        stateService.UpdateDeviceState(device.Guid, stateDto);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error broadcasting device state");
            }
        }

        logger.LogInformation("DeviceStateBroadcaster stopped");
    }

    /// <summary>
    /// Pattern match on device type and map to appropriate combined DTO
    /// </summary>
    private DeviceDto? MapDeviceToStateDto(IDevice device)
    {
        return device switch
        {
            PdmMaxDevice pdmMax => MapPdmMaxDto(pdmMax),
            PdmDevice pdm => MapPdmDto(pdm),
            CanboardDevice canboard => MapCanboardDto(canboard),
            _ => null
        };
    }

    private PdmDto MapPdmDto(PdmDevice device)
    {
        // Map base device properties using AutoMapper
        var dto = mapper.Map<PdmDto>(device);

        // Map collections using accessor methods
        dto.Inputs = mapper.Map<List<InputDto>>(device.GetInputs());
        dto.Outputs = mapper.Map<List<OutputDto>>(device.GetOutputs());
        dto.CanInputs = mapper.Map<List<CanInputDto>>(device.GetCanInputs());
        dto.VirtualInputs = mapper.Map<List<VirtualInputDto>>(device.GetVirtualInputs());
        dto.Flashers = mapper.Map<List<FlasherDto>>(device.GetFlashers());
        dto.Counters = mapper.Map<List<CounterDto>>(device.GetCounters());
        dto.Conditions = mapper.Map<List<ConditionDto>>(device.GetConditions());

        // Map single objects
        dto.Wipers = mapper.Map<WiperDto>(device.GetWipers());

        return dto;
    }

    private PdmMaxDto MapPdmMaxDto(PdmMaxDevice device)
    {
        // PdmMax inherits from PdmDevice, so use the base mapping
        var pdmDto = MapPdmDto(device);

        // Map to PdmMaxDto (which currently inherits from PdmDto)
        var dto = mapper.Map<PdmMaxDto>(device);

        // Copy properties from base mapping
        dto.Inputs = pdmDto.Inputs;
        dto.Outputs = pdmDto.Outputs;
        dto.CanInputs = pdmDto.CanInputs;
        dto.VirtualInputs = pdmDto.VirtualInputs;
        dto.Flashers = pdmDto.Flashers;
        dto.Counters = pdmDto.Counters;
        dto.Conditions = pdmDto.Conditions;
        dto.Wipers = pdmDto.Wipers;

        return dto;
    }

    private CanboardDto MapCanboardDto(CanboardDevice device)
    {
        // Placeholder for CANBoard DTO mapping
        // Will be expanded when CanboardDevice is fully implemented
        var dto = mapper.Map<CanboardDto>(device);

        return dto;
    }

}
