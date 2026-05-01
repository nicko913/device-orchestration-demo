using DeviceOrchestrationDemo.Core;

namespace DeviceOrchestrationDemo.Infrastructure;

/// <summary>
/// Holds all registered device drivers, resolved by device ID at runtime.
///
/// Adding a new device type means implementing IDeviceDriver and calling Register()
/// in Program.cs — no changes to routing or API code needed.
/// </summary>
public sealed class DeviceRegistry
{
    private readonly Dictionary<string, IDeviceDriver> _drivers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(IDeviceDriver driver) =>
        _drivers[driver.DeviceId] = driver;

    public IDeviceDriver? Resolve(string deviceId) =>
        _drivers.GetValueOrDefault(deviceId);

    public IReadOnlyList<IDeviceDriver> All =>
        _drivers.Values.ToList();
}
