using DeviceOrchestrationDemo.Core;
using DeviceOrchestrationDemo.Infrastructure;

namespace DeviceOrchestrationDemo.Api;

/// <summary>
/// Bridges the Northbound API endpoints to the DeviceRegistry and device drivers.
/// Adapted from the production NorthboundDeviceService, with the license gate and
/// telemetry removed so the architectural pattern is clear.
/// </summary>
public sealed class DeviceService
{
    private readonly DeviceRegistry _registry;
    private readonly IReadOnlyDictionary<string, string> _deviceNames;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(DeviceRegistry registry, VenueConfig config, ILogger<DeviceService> logger)
    {
        _registry = registry;
        _logger = logger;

        // Build a deviceId → friendly name lookup from all config sections
        _deviceNames = config.Zones
            .Select(z => (z.DeviceId, z.Name))
            .Concat(config.Sources.Select(s => (s.DeviceId, s.Name)))
            .Concat(config.AudioZones.Select(a => (a.DeviceId, a.Name)))
            .ToDictionary(x => x.DeviceId, x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken ct = default)
    {
        var dtos = new List<DeviceDto>();
        foreach (var driver in _registry.All)
        {
            var state = await driver.GetStateAsync(ct);
            dtos.Add(ToDto(driver, state));
        }
        return dtos;
    }

    public async Task<DeviceDto?> GetDeviceAsync(string id, CancellationToken ct = default)
    {
        var driver = _registry.Resolve(id);
        if (driver is null) return null;

        var state = await driver.GetStateAsync(ct);
        return ToDto(driver, state);
    }

    public async Task<CommandResponseDto> ExecuteCommandAsync(
        string id,
        CommandRequestDto request,
        CancellationToken ct = default)
    {
        var driver = _registry.Resolve(id);
        if (driver is null)
        {
            return new CommandResponseDto(false, id, request.Command,
                $"Device '{id}' not found.", new Dictionary<string, object?>());
        }

        var command = new CommandRequest(
            request.Command,
            request.Parameters ?? new Dictionary<string, string>());

        var result = await driver.ExecuteAsync(command, ct);
        var state = await driver.GetStateAsync(ct);

        _logger.LogInformation(
            "Command '{Command}' on device '{DeviceId}': {Outcome}",
            request.Command, id,
            result.Success ? "ok" : result.Error);

        return new CommandResponseDto(result.Success, id, request.Command, result.Error, state.Values);
    }

    private DeviceDto ToDto(Core.IDeviceDriver driver, Core.DeviceState state) =>
        new(driver.DeviceId,
            _deviceNames.GetValueOrDefault(driver.DeviceId, driver.DeviceId),
            driver.DeviceType,
            driver.Capabilities,
            state.Values);
}
