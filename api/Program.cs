using application.ConfigFile;
using application.Devices;
using domain.Interfaces;
using infrastructure.BackgroundServices;
using infrastructure.Comms;
using infrastructure.Comms.Adapters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(DeviceManager).Assembly);
});

// Add services to the container.
builder.Services.AddTransient<UsbAdapter>();
builder.Services.AddTransient<SlcanAdapter>();
builder.Services.AddTransient<PcanAdapter>();
builder.Services.AddTransient<SimAdapter>();

builder.Services.AddSingleton<ICommsAdapterManager,  CommsAdapterManager>();
builder.Services.AddSingleton<ConfigFileManager>();
builder.Services.AddSingleton<DeviceManager>();

builder.Services.AddHostedService<CommsDataPipeline>();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();