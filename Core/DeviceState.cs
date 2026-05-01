namespace DeviceOrchestrationDemo.Core;

public sealed record DeviceState(
    string DeviceId,
    IReadOnlyDictionary<string, object?> Values);
