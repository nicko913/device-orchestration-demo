namespace DeviceOrchestrationDemo.Core;

public sealed record CommandRequest(
    string Command,
    IReadOnlyDictionary<string, string> Parameters)
{
    public static CommandRequest Create(string command, params (string key, string value)[] parameters) =>
        new(command, parameters.ToDictionary(p => p.key, p => p.value));
}
