using DeviceOrchestrationDemo.Core;

namespace DeviceOrchestrationDemo.Drivers;

/// <summary>
/// Simulates an audio zone (amplifier zone or DSP channel).
/// In production this would send volume/mute commands over TCP, RS-232, or a
/// vendor-specific protocol to the audio hardware.
/// </summary>
public sealed class MockAudioZoneDriver : IDeviceDriver
{
    private int _volume;
    private bool _muted;

    public string DeviceId { get; }
    public string DeviceType => "audio_zone";
    public IReadOnlyList<string> Capabilities => [DeviceCapabilities.VolumeControl];

    public MockAudioZoneDriver(string deviceId, int defaultVolume = 50)
    {
        DeviceId = deviceId;
        _volume = defaultVolume;
    }

    public Task<CommandResult> ExecuteAsync(CommandRequest command, CancellationToken ct = default)
    {
        var result = command.Command.ToLowerInvariant() switch
        {
            "setvolume" => SetVolume(command.Parameters),
            "setmute"   => SetMute(command.Parameters),
            _ => CommandResult.Fail($"Unsupported command '{command.Command}' for audio zone.")
        };

        return Task.FromResult(result);
    }

    public Task<DeviceState> GetStateAsync(CancellationToken ct = default) =>
        Task.FromResult(new DeviceState(DeviceId, new Dictionary<string, object?>
        {
            ["volume"] = _volume,
            ["muted"]  = _muted
        }));

    private CommandResult SetVolume(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("level", out var raw) || !int.TryParse(raw, out var level))
            return CommandResult.Fail("'level' must be an integer.");

        if (level is < 0 or > 100)
            return CommandResult.Fail("'level' must be between 0 and 100.");

        _volume = level;
        return CommandResult.Ok();
    }

    private CommandResult SetMute(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("on", out var raw) || !bool.TryParse(raw, out var value))
            return CommandResult.Fail("'on' must be 'true' or 'false'.");

        _muted = value;
        return CommandResult.Ok();
    }
}
