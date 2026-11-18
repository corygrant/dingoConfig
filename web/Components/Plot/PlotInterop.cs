using web.Services;

namespace web.Components.Plot;

internal static class PlotInterop
{
    public static string ModulePath(IWebHostEnvironment env) => Utils.ModuleUrl(env, "js/plot/plot.js");
}
