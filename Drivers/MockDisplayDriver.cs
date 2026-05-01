using DeviceOrchestrationDemo.Core;

namespace DeviceOrchestrationDemo.Drivers;

/// <summary>
/// Simulates a display/TV in a zone (e.g. a bar TV or dining room screen).
/// In production this would send CEC, RS-232, or IP commands to the physical display.
/// </summary>
public sealed class MockDisplayDriver : IDeviceDriver
{
    private bool _power;
    private string? _currentInput;

    public string DeviceId { get; }
    public string DeviceType => "display";
    public IReadOnlyList<string> Capabilities =>
        [DeviceCapabilities.Power, DeviceCapabilities.InputSelect];

    public MockDisplayDriver(string deviceId) => DeviceId = deviceId;

    public Task<CommandResult> ExecuteAsync(CommandRequest command, CancellationToken ct = default)
    {
        var result = command.Command.ToLowerInvariant() switch
        {
            "setpower"    => SetPower(command.Parameters),
            "selectinput" => SelectInput(command.Parameters),
            _ => CommandResult.Fail($"Unsupported command '{command.Command}' for display.")
        };

        return Task.FromResult(result);
    }

    public Task<DeviceState> GetStateAsync(CancellationToken ct = default) =>
        Task.FromResult(new DeviceState(DeviceId, new Dictionary<string, object?>
        {
            ["power"] = _power,
            ["input"] = _currentInput
        }));

    private CommandResult SetPower(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("on", out var raw))
            return CommandResult.Fail("Missing 'on' parameter.");

        if (!bool.TryParse(raw, out var value))
            return CommandResult.Fail($"'on' must be 'true' or 'false', got '{raw}'.");

        _power = value;
        return CommandResult.Ok();
    }

    private CommandResult SelectInput(IReadOnlyDictionary<string, string> parameters)
    {
        _currentInput = parameters.GetValueOrDefault("sourceId");
        return CommandResult.Ok();
    }
}
