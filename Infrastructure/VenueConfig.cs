namespace DeviceOrchestrationDemo.Infrastructure;

public sealed class VenueConfig
{
    public List<ZoneConfig> Zones { get; init; } = [];
    public List<SourceConfig> Sources { get; init; } = [];
    public List<AudioZoneConfig> AudioZones { get; init; } = [];
}

public sealed class ZoneConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>ID of the IDeviceDriver that controls the display in this zone.</summary>
    public string DeviceId { get; init; } = string.Empty;
}

public sealed class SourceConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>ID of the IDeviceDriver (matrix input) that feeds this source.</summary>
    public string DeviceId { get; init; } = string.Empty;
}

public sealed class AudioZoneConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public int DefaultVolume { get; init; } = 50;
}
