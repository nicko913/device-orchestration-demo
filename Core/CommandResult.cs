namespace DeviceOrchestrationDemo.Core;

public sealed record CommandResult(bool Success, string? Error = null)
{
    public static CommandResult Ok() => new(true);
    public static CommandResult Fail(string error) => new(false, error);
}
