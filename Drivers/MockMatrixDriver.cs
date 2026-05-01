using DeviceOrchestrationDemo.Core;

namespace DeviceOrchestrationDemo.Drivers;

/// <summary>
/// Simulates one input port on a video matrix switcher.
/// In production this would send routing commands to hardware (e.g. TCP ASCII commands
/// to a matrix switcher) to connect this input to the requested output.
/// </summary>
public sealed class MockMatrixDriver : IDeviceDriver
{
    private string? _currentDestination;

    public string DeviceId { get; }
    public string DeviceType => "matrix";
    public IReadOnlyList<string> Capabilities => [DeviceCapabilities.InputSelect];

    public MockMatrixDriver(string deviceId) => DeviceId = deviceId;

    public Task<CommandResult> ExecuteAsync(CommandRequest command, CancellationToken ct = default)
    {
        if (!command.Command.Equals("switchOutput", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(CommandResult.Fail($"Unsupported command '{command.Command}' for matrix."));

        _currentDestination = command.Parameters.GetValueOrDefault("destinationId");
        return Task.FromResult(CommandResult.Ok());
    }

    public Task<DeviceState> GetStateAsync(CancellationToken ct = default) =>
        Task.FromResult(new DeviceState(DeviceId, new Dictionary<string, object?>
        {
            ["currentDestination"] = _currentDestination
        }));
}
