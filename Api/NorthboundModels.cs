namespace DeviceOrchestrationDemo.Api;

public sealed record DeviceDto(
    string Id,
    string Name,
    string Type,
    IReadOnlyList<string> Capabilities,
    IReadOnlyDictionary<string, object?> State);

public sealed record CommandRequestDto(
    string Command,
    Dictionary<string, string>? Parameters);

public sealed record CommandResponseDto(
    bool Success,
    string DeviceId,
    string Command,
    string? Error,
    IReadOnlyDictionary<string, object?> State);

/// <summary>
/// Posted to /api/route to drive the CommandRouter without going
/// through a specific device ID.
/// </summary>
public sealed record RouterCommandDto(
    string Action,
    string? TargetId,
    Dictionary<string, string>? Parameters);
