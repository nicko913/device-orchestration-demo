using DeviceOrchestrationDemo.Core;
using DeviceOrchestrationDemo.Infrastructure;

namespace DeviceOrchestrationDemo.Execution;

/// <summary>
/// Routes named commands to the appropriate device driver.
///
/// Adapted from the production JoinDispatcher, which routes Crestron join events
/// to hardware actions. Here the concept is the same but expressed as named actions
/// rather than hardware-specific join numbers, making the pattern easier to follow.
///
/// The SemaphoreSlim serializes state mutations when multiple panels are connected
/// simultaneously — without it, two concurrent "route" commands could read stale
/// state and produce conflicting hardware calls.
/// </summary>
public sealed class CommandRouter
{
    private readonly VenueConfig _config;
    private readonly DeviceRegistry _registry;
    private readonly ILogger<CommandRouter> _logger;

    // Serializes state mutations across concurrent panel connections
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _currentZoneId;
    private string? _currentSourceId;

    // Tracks which source is currently routed to each zone
    private readonly Dictionary<string, string> _sourceAtZone =
        new(StringComparer.OrdinalIgnoreCase);

    public CommandRouter(VenueConfig config, DeviceRegistry registry, ILogger<CommandRouter> logger)
    {
        _config = config;
        _registry = registry;
        _logger = logger;
    }

    public async Task<CommandResult> RouteAsync(
        string action,
        string? targetId = null,
        IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return action.ToLowerInvariant() switch
            {
                "selectzone"   => HandleZoneSelect(targetId),
                "selectsource" => HandleSourceSelect(targetId),
                "routesource"  => await HandleRouteAsync(ct),
                "poweron"      => await HandlePowerAsync(on: true, ct),
                "poweroff"     => await HandlePowerAsync(on: false, ct),
                _              => CommandResult.Fail($"Unknown action '{action}'.")
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Current zone → source routing table, for the Northbound API to read.</summary>
    public IReadOnlyDictionary<string, string> RoutingTable => _sourceAtZone;

    // ── Zone select ───────────────────────────────────────────────────────────

    private CommandResult HandleZoneSelect(string? zoneId)
    {
        var zone = _config.Zones.FirstOrDefault(z =>
            string.Equals(z.Id, zoneId, StringComparison.OrdinalIgnoreCase));

        if (zone == null)
        {
            _logger.LogWarning("Zone '{ZoneId}' not found in config", zoneId);
            return CommandResult.Fail($"Zone '{zoneId}' not found.");
        }

        _currentZoneId = zone.Id;
        _logger.LogInformation("Zone selected: {ZoneName}", zone.Name);
        return CommandResult.Ok();
    }

    // ── Source select ─────────────────────────────────────────────────────────

    private CommandResult HandleSourceSelect(string? sourceId)
    {
        var source = _config.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));

        if (source == null)
        {
            _logger.LogWarning("Source '{SourceId}' not found in config", sourceId);
            return CommandResult.Fail($"Source '{sourceId}' not found.");
        }

        _currentSourceId = source.Id;
        _logger.LogInformation("Source selected: {SourceName}", source.Name);
        return CommandResult.Ok();
    }

    // ── Route source → zone ───────────────────────────────────────────────────

    private async Task<CommandResult> HandleRouteAsync(CancellationToken ct)
    {
        if (_currentZoneId is null) return CommandResult.Fail("No zone selected. Call selectZone first.");
        if (_currentSourceId is null) return CommandResult.Fail("No source selected. Call selectSource first.");

        var zone = _config.Zones.FirstOrDefault(z => z.Id == _currentZoneId);
        var source = _config.Sources.FirstOrDefault(s => s.Id == _currentSourceId);

        if (zone is null || source is null)
            return CommandResult.Fail("Zone or source no longer present in config.");

        var displayDriver = _registry.Resolve(zone.DeviceId);
        var matrixDriver = _registry.Resolve(source.DeviceId);

        if (displayDriver is null)
        {
            _logger.LogWarning("No driver registered for display device '{DeviceId}'", zone.DeviceId);
            return CommandResult.Fail($"No driver registered for zone display '{zone.DeviceId}'.");
        }

        if (matrixDriver is null)
        {
            _logger.LogWarning("No driver registered for matrix device '{DeviceId}'", source.DeviceId);
            return CommandResult.Fail($"No driver registered for source matrix '{source.DeviceId}'.");
        }

        // Step 1: switch the matrix to route this source to this zone's display
        var matrixResult = await matrixDriver.ExecuteAsync(
            CommandRequest.Create("switchOutput", ("destinationId", zone.DeviceId)), ct);

        if (!matrixResult.Success)
            return matrixResult;

        // Step 2: tell the display which input is now active
        var displayResult = await displayDriver.ExecuteAsync(
            CommandRequest.Create("selectInput", ("sourceId", source.Id)), ct);

        if (displayResult.Success)
        {
            _sourceAtZone[zone.Id] = source.Id;
            _logger.LogInformation("Routed {SourceName} → {ZoneName}", source.Name, zone.Name);
        }

        return displayResult;
    }

    // ── Power ─────────────────────────────────────────────────────────────────

    private async Task<CommandResult> HandlePowerAsync(bool on, CancellationToken ct)
    {
        var command = CommandRequest.Create("setPower", ("on", on.ToString().ToLower()));
        var failures = 0;

        foreach (var zone in _config.Zones)
        {
            var driver = _registry.Resolve(zone.DeviceId);
            if (driver is null) continue;

            var result = await driver.ExecuteAsync(command, ct);
            if (!result.Success) failures++;

            _logger.LogInformation(
                "Power {State} → {ZoneName}: {Outcome}",
                on ? "on" : "off", zone.Name,
                result.Success ? "ok" : result.Error);
        }

        return failures == 0
            ? CommandResult.Ok()
            : CommandResult.Fail($"{failures} zone(s) failed to power {(on ? "on" : "off")}.");
    }
}
