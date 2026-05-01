using DeviceOrchestrationDemo.Execution;

namespace DeviceOrchestrationDemo.Api;

/// <summary>
/// The four Northbound API endpoints — adapted from the production system's
/// NorthboundEndpointRouteBuilderExtensions, with telemetry calls removed.
///
/// These endpoints expose only canonical device models. External integrations
/// (HomeKit, dashboards, etc.) consume this API without knowing anything about
/// the underlying hardware protocol.
/// </summary>
public static class NorthboundEndpoints
{
    public static IEndpointRouteBuilder MapNorthboundApi(this IEndpointRouteBuilder app)
    {
        // List all registered devices with their current state
        app.MapGet("/api/devices", async (DeviceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDevicesAsync(ct)));

        // Get state for one device
        app.MapGet("/api/devices/{id}/state", async (string id, DeviceService svc, CancellationToken ct) =>
        {
            var device = await svc.GetDeviceAsync(id, ct);
            return device is null
                ? Results.NotFound(new { error = $"Device '{id}' not found." })
                : Results.Ok(device.State);
        });

        // Send a command directly to a device by ID
        app.MapPost("/api/devices/{id}/command", async (
            string id,
            CommandRequestDto request,
            DeviceService svc,
            CancellationToken ct) =>
        {
            var response = await svc.ExecuteCommandAsync(id, request, ct);
            return response.Success ? Results.Ok(response) : Results.BadRequest(response);
        });

        // Drive the CommandRouter (zone select → source select → route)
        app.MapPost("/api/route", async (
            RouterCommandDto request,
            CommandRouter router,
            CancellationToken ct) =>
        {
            var result = await router.RouteAsync(
                request.Action, request.TargetId, request.Parameters, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        // Inspect the current zone → source routing table
        app.MapGet("/api/routing-table", (CommandRouter router) =>
            Results.Ok(router.RoutingTable));

        return app;
    }
}
