namespace DeviceOrchestrationDemo.Core;

/// <summary>
/// Contract for any controllable device in the system.
///
/// The CommandRouter and API layer resolve drivers from the registry by device ID —
/// they never import a concrete driver class. This means swapping hardware
/// (or adding a mock for testing) requires adding one class and one config entry,
/// with no changes to routing or API code.
/// </summary>
public interface IDeviceDriver
{
    string DeviceId { get; }

    /// <summary>"display", "matrix", "audio_zone"</summary>
    string DeviceType { get; }

    IReadOnlyList<string> Capabilities { get; }

    Task<CommandResult> ExecuteAsync(CommandRequest command, CancellationToken ct = default);
    Task<DeviceState> GetStateAsync(CancellationToken ct = default);
}
