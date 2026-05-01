using System.Text.Json;

namespace DeviceOrchestrationDemo.Infrastructure;

public static class VenueConfigLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static VenueConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Venue config not found at: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VenueConfig>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize venue config.");
    }
}
