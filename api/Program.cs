using application.Services;
using infrastructure.Adapters;
using infrastructure.BackgroundServices;
using infrastructure.Comms;
using api.Components;
using MudBlazor.Services;
using domain.Interfaces;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Add HttpClient for Blazor Server to call local API
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

// Add API services
builder.Services.AddTransient<UsbAdapter>();
builder.Services.AddTransient<SlcanAdapter>();
builder.Services.AddTransient<PcanAdapter>();
builder.Services.AddTransient<SimAdapter>();

builder.Services.AddSingleton<ICommsAdapterManager, CommsAdapterManager>();
builder.Services.AddSingleton<ConfigFileManager>();
builder.Services.AddSingleton<DeviceManager>();

// Add background services
builder.Services.AddHostedService<CommsDataPipeline>();

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

app.Run();
