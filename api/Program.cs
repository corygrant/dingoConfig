using application.Services;
using infrastructure.Adapters;
using infrastructure.BackgroundServices;
using infrastructure.Comms;
using infrastructure.Logging;
using api.Components;
using api.Services;
using MudBlazor.Services;
using domain.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

// Configure host shutdown timeout
builder.Host.ConfigureHostOptions(opts =>
{
    opts.ShutdownTimeout = TimeSpan.FromSeconds(5);
});

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;

    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
});

// Add HttpClient for Blazor Server to call local API
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

// Add NotificationService for combined Snackbar + GlobalLogger calls
builder.Services.AddScoped<NotificationService>();

// Add API services
builder.Services.AddTransient<UsbAdapter>();
builder.Services.AddTransient<SlcanAdapter>();
builder.Services.AddTransient<PcanAdapter>();
builder.Services.AddTransient<SimAdapter>();

builder.Services.AddSingleton<ICommsAdapterManager, CommsAdapterManager>();
builder.Services.AddSingleton<ConfigFileManager>();
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddSingleton<CanMsgLogger>();
builder.Services.AddSingleton<GlobalLogger>();

// Add background services
builder.Services.AddHostedService<CommsDataPipeline>();

// Add GlobalLogger to logging pipeline
var serviceProvider = builder.Services.BuildServiceProvider();
var globalLogger = serviceProvider.GetRequiredService<GlobalLogger>();
builder.Logging.AddProvider(new GlobalLoggerProvider(globalLogger));

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

// Initialize ConfigFileManager to ensure working directory exists
var configFileManager = app.Services.GetRequiredService<ConfigFileManager>();
configFileManager.Initialize();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    // Enable Swagger in development
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAntiforgery();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Open browser on launch in non-Development environments (e.g., published Release builds)
var isDevelopment = app.Environment.IsDevelopment();
if (!isDevelopment)
{
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() => OpenBrowser("http://localhost:5000"));
}

try
{
    app.Run();
}
catch (IOException ex) when (ex.InnerException is AddressInUseException)
{
    // Port is already in use - app is already running, just open browser
    if (!isDevelopment)
    {
        OpenBrowser("http://localhost:5000");
    }
}

static void OpenBrowser(string url)
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            System.Diagnostics.Process.Start("xdg-open", url);
        }
        else if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", url);
        }
    }
    catch
    {
        // Browser launch failed - app will still run
    }
}
