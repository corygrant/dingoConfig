using application.Services;

namespace web.Services;

/// <summary>
/// Opens a config file into the device manager. Shared by the file toolbar and
/// the recent projects list on the home page, so both behave the same.
/// </summary>
public class ProjectService(ConfigFileManager fileManager, DeviceManager deviceManager, NotificationService notification)
{
    public async Task<bool> OpenAsync(string fileName)
    {
        try
        {
            var devices = await fileManager.LoadDevices(fileName);
            if (devices is { Count: > 0 })
            {
                deviceManager.ClearDevices();
                deviceManager.AddDevices(devices);
                notification.NewSuccess($"Opened {Path.GetFileName(fileName)} - {devices.Count} device(s) loaded");

                foreach (var device in devices)
                    notification.NewSuccess($"Loaded device: {device.Name}, ID: {device.BaseId}");

                return true;
            }

            notification.NewWarning($"No devices found in {Path.GetFileName(fileName)}");
        }
        catch (Exception ex)
        {
            notification.NewError($"Error opening file: {fileName}", ex);
        }

        return false;
    }
}
