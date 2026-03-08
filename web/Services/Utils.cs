using application.Models;

namespace web.Services;

public static class Utils
{
    public static string FormatId(int id, NumberFormat format)
    {
        if (format == NumberFormat.Hex)
            return "0x" + id.ToString("X3");
        return id.ToString("D");
    }

    public static int ParseId(string id, NumberFormat format, NotificationService notification)
    {
        try
        {
            int val;
            if (format == NumberFormat.Hex)
                val = Convert.ToInt32(id, fromBase: 16);
            else
                val = Convert.ToInt32(id);

            if (val < 0 || val > 2047)
                notification.NewWarning("ID must be 0 to 0x7FF (2047)");

            return Math.Clamp(val, 0, 2047);
        }
        catch (ArgumentException e) { notification.NewError("Invalid format", e); }
        catch (FormatException e) { notification.NewError("Invalid character", e); }
        catch (OverflowException e) { notification.NewError("Value overflow", e); }
        return 0;
    }
}
