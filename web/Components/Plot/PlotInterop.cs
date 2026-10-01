namespace web.Components.Plot;

internal static class PlotInterop
{
    // Version query busts the browser cache when the app is updated
    public static readonly string ModulePath =
        $"./js/plot/plot.js?v={typeof(PlotInterop).Assembly.GetName().Version}";
}
