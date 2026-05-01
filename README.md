# Device Orchestration Middleware — Architecture Demo

A portfolio demonstration of a middleware architecture for orchestrating network-connected AV/IT devices. Built to show clean architectural separation, interface-driven device abstraction, and configuration-driven command routing.

> This is a simplified, runnable demonstration inspired by a production AV control platform deployed across hospitality venues. Real hardware drivers, licensing, and venue-specific configuration have been replaced with mock implementations and genericized config.

---

## What This Demonstrates

| Pattern | Where |
|---------|-------|
| Middleware / broker pattern | `CommandRouter` decouples panels from hardware |
| Interface-driven device abstraction | `IDeviceDriver` — swap any hardware without touching routing |
| Configuration-driven routing | Add zones and sources in JSON — zero code changes |
| Canonical external API | REST + WebSocket Northbound API for third-party integrations |
| Extensible driver model | New device types are registered, not hard-coded |

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       Control Surfaces                          │
│                                                                 │
│   WPF Touchscreen Panel          Browser / Mobile Web           │
│         (TCP text)                   (WebSocket)                │
└──────────────┬──────────────────────────┬───────────────────────┘
               │                          │
               ▼                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Middleware Service                           │
│                                                                 │
│   ┌─────────────┐    ┌─────────────┐    ┌───────────────────┐  │
│   │ Command     │───▶│  Device     │───▶│   Driver Engine   │  │
│   │ Router      │    │  Registry   │    │   (dispatch)      │  │
│   └─────────────┘    └─────────────┘    └────────┬──────────┘  │
│                                                   │             │
│   ┌──────────────────────────────────┐            │             │
│   │  Northbound API (REST + WS)      │◀───────────┘             │
│   │  /api/devices  /api/events       │                          │
│   └──────────────────────────────────┘                          │
└──────────────────────────┬──────────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          ▼                ▼                ▼
   IDeviceDriver    IDeviceDriver    IDeviceDriver
   (Display/TV)    (Matrix Switch)  (Audio Zone)
      [Mock]           [Mock]          [Mock]

                           │
          ┌────────────────┼────────────────┐
          ▼                ▼                ▼
   Apple HomeKit      NOC Dashboard    Any REST Client
  (via Homebridge)
```

---

## Key Design Decisions

### 1. Interface-Driven Hardware Abstraction

Every device implements `IDeviceDriver` — a contract that exposes capabilities (`Power`, `InputSelect`, `VolumeControl`) independent of vendor protocol. The `CommandRouter` and `DriverEngine` never import a concrete driver; they resolve from the registry by device ID.

```csharp
public interface IDeviceDriver
{
    string DeviceId { get; }
    string DeviceType { get; }     // "display", "matrix", "audio_zone"
    IReadOnlyList<DeviceCapability> Capabilities { get; }

    Task<CommandResult> ExecuteAsync(CommandRequest command, CancellationToken ct = default);
    Task<DeviceState> GetStateAsync(CancellationToken ct = default);
}
```

**Why:** Swapping a matrix switcher vendor, or adding a software mock for testing, means adding one class and one config entry — no changes to routing logic, no changes to the external API. In the production system this was built from, this abstraction allowed adding Apple HomeKit support in a single afternoon without touching any panel or hardware code.

---

### 2. Configuration-Driven Routing

Zones, sources, destinations, and device mappings are declared in `venue.json`. The `CommandRouter` reads this at startup and builds its dispatch table dynamically. Adding a new zone is an ops task, not a dev task.

```json
{
  "zones": [
    { "id": "bar",    "name": "Bar",    "deviceId": "display-bar-1"    },
    { "id": "dining", "name": "Dining", "deviceId": "display-dining-1" },
    { "id": "patio",  "name": "Patio",  "deviceId": "display-patio-1"  }
  ],
  "sources": [
    { "id": "cable-1",   "name": "Cable 1",   "deviceId": "matrix-input-1" },
    { "id": "streaming", "name": "Apple TV",  "deviceId": "matrix-input-2" }
  ],
  "audioZones": [
    {
      "id": "bar-audio", "name": "Bar Audio",
      "deviceId": "audio-zone-1", "defaultVolume": 40
    }
  ]
}
```

**Why:** In a hospitality deployment, venue layout changes (adding a patio screen, swapping a cable box) happen constantly. Requiring a code deployment for each change is a support burden. With config-driven routing, a non-developer can make these changes in the field.

---

### 3. Canonical Northbound API

External integrations — Apple HomeKit, NOC dashboards, third-party automation — consume a REST + WebSocket API that exposes only canonical device models: `display`, `power`, `input_select`. They never see vendor-specific protocol details.

```
GET  /api/devices               → list all devices with capabilities
GET  /api/devices/{id}/state    → current state of one device
POST /api/devices/{id}/command  → send a canonical command
GET  /api/events (WebSocket)    → snapshot on connect, then stateChanged events
```

Example command:
```json
POST /api/devices/display-bar-1/command
{
  "command": "setPower",
  "parameters": { "on": true }
}
```

**Why:** A Homebridge plugin written against this API works regardless of what hardware sits behind the service. The HomeKit integration doesn't know whether the display is controlled by TCP, IR, RS-232, or a relay — and it shouldn't have to. This separation also means the external API is stable across hardware swaps.

---

### 4. Why a Separate Middleware Service (Not Direct Panel→Hardware)

An alternative design would have control panels talk directly to AV hardware. This was rejected for three reasons:

1. **Single hardware connection:** Many AV devices (matrix switchers, processors) accept only one TCP connection at a time. A middleware service brokers a single hardware connection shared across all panels.
2. **Multi-client sync:** When Panel A changes a source, Panel B's UI should update immediately. The middleware broadcasts state changes to all connected panels; direct connections cannot do this.
3. **Hardware resilience:** The service maintains hardware connections persistently, with auto-reconnect. Panels come and go; the hardware connection doesn't.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8, C# 12 |
| Architecture | Clean Architecture (Core → Infrastructure → Execution) |
| Device Drivers | Interface-based, mock implementations included |
| Config | JSON (`venue.json`) |
| External API | ASP.NET Core Minimal API + WebSocket |
| Testing | xUnit, mock driver implementations |

---

## Project Structure

```
src/
├── Core/                   Pure domain — interfaces and models, no dependencies
│   ├── IDeviceDriver.cs
│   ├── CommandRequest.cs
│   ├── CommandResult.cs
│   ├── DeviceCapability.cs
│   └── DeviceState.cs
│
├── Infrastructure/         Config loading and device registry
│   ├── VenueConfigLoader.cs
│   ├── DeviceRegistry.cs
│   └── VenueConfig.cs      (deserialization target for venue.json)
│
├── Execution/              Command routing and driver dispatch
│   ├── CommandRouter.cs
│   └── DriverEngine.cs
│
├── Drivers/                Mock implementations (runnable without hardware)
│   ├── MockDisplayDriver.cs
│   ├── MockMatrixDriver.cs
│   └── MockAudioZoneDriver.cs
│
└── Api/                    Northbound REST + WebSocket
    ├── NorthboundEndpoints.cs
    └── NorthboundModels.cs

config/
└── venue.example.json      Genericized venue configuration (copy to venue.json to run)
```

---

## Quickstart

```bash
git clone https://github.com/nicko913/device-orchestration-demo
cd device-orchestration-demo

# Copy the example config
cp config/venue.example.json config/venue.json

dotnet run

# API is now live at http://localhost:5000
```

Try it:
```bash
# List all registered devices and their current state
curl http://localhost:5000/api/devices

# Get the state of the bar display
curl http://localhost:5000/api/devices/display-bar/state

# Power on all displays
curl -X POST http://localhost:5000/api/devices/display-bar/command \
  -H "Content-Type: application/json" \
  -d '{"command": "setPower", "parameters": {"on": "true"}}'

# Route a source to a zone (select zone → select source → route)
curl -X POST http://localhost:5000/api/route \
  -H "Content-Type: application/json" \
  -d '{"action": "selectZone", "targetId": "bar"}'

curl -X POST http://localhost:5000/api/route \
  -H "Content-Type: application/json" \
  -d '{"action": "selectSource", "targetId": "cable-1"}'

curl -X POST http://localhost:5000/api/route \
  -H "Content-Type: application/json" \
  -d '{"action": "routeSource"}'

# Inspect the current routing table
curl http://localhost:5000/api/routing-table
```

---

## Production Context

This demo is extracted from a production system — **AVConductorSuite** — a commercial AV control platform deployed across hospitality venues (bars, restaurants, sports bars). The full system adds:

- A WPF touchscreen panel (wall-mounted, 1920×1080) and a browser-based companion (staff phones)
- A hardware driver for Wyrestorm NHD-CTL matrix switchers over raw TCP
- A Crestron SIMPL+ integration layer for legacy processors
- A cloud license server with machine fingerprinting and 72-hour offline grace
- A Homebridge plugin that exposes display devices as Apple TV accessories in HomeKit
- A live traffic telemetry dashboard for per-site diagnostics
- A suite of WPF integrator tools (config editor, setup wizard, device driver editor)

The architectural patterns here — interface abstraction, config-driven routing, and the canonical Northbound API — are the same patterns that make the full system's complexity manageable.

---

## What's Intentionally Omitted

| Omitted | Reason |
|---------|--------|
| Real hardware drivers | Replaced with mocks — hardware is incidental to the patterns |
| Licensing / machine fingerprinting | Not relevant to architectural demonstration |
| WPF and web panel UIs | UI code adds noise; the middleware is the story |
| Vendor-specific TCP protocols | The abstraction layer is the point, not what's behind it |
| Windows service install / deployment config | Production ops detail, not architecture |
