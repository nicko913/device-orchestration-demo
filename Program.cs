using DeviceOrchestrationDemo.Api;
using DeviceOrchestrationDemo.Drivers;
using DeviceOrchestrationDemo.Execution;
using DeviceOrchestrationDemo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Load venue config ─────────────────────────────────────────────────────────

var configPath = builder.Configuration["VenueConfigPath"]
    ?? Path.Combine("config", "venue.json");

VenueConfig config;
try
{
    config = VenueConfigLoader.Load(configPath);
    Console.WriteLine($"Loaded: {config.Zones.Count} zones, {config.Sources.Count} sources, {config.AudioZones.Count} audio zones");
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine($"Venue config not found at '{configPath}'.");
    Console.Error.WriteLine("Copy config/venue.example.json to config/venue.json and try again.");
    return;
}

// ── Build device registry ─────────────────────────────────────────────────────
//
// One mock driver is registered per device in config. In production, concrete
// drivers (e.g. WyrestormMatrixDriver, CecDisplayDriver) are registered here instead.
// The routing and API layers never see this switch — they resolve by device ID.

var registry = new DeviceRegistry();

foreach (var zone in config.Zones)
    registry.Register(new MockDisplayDriver(zone.DeviceId));

foreach (var source in config.Sources)
    registry.Register(new MockMatrixDriver(source.DeviceId));

foreach (var audio in config.AudioZones)
    registry.Register(new MockAudioZoneDriver(audio.DeviceId, audio.DefaultVolume));

// ── DI registrations ──────────────────────────────────────────────────────────

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(registry);
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<DeviceService>();

// ── Build and configure app ───────────────────────────────────────────────────

var app = builder.Build();

app.MapNorthboundApi();
app.MapGet("/", () => Results.Redirect("/api/devices"));

Console.WriteLine("Device Orchestration Demo running at http://localhost:5000");
Console.WriteLine("Endpoints:");
Console.WriteLine("  GET  /api/devices");
Console.WriteLine("  GET  /api/devices/{id}/state");
Console.WriteLine("  POST /api/devices/{id}/command");
Console.WriteLine("  POST /api/route");
Console.WriteLine("  GET  /api/routing-table");

app.Run();
