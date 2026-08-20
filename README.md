# iot_hobby

IoT temperature & humidity monitoring solution: a simulated ESP32 publishes sensor
readings over MQTT, an API ingests them into SQLite and pushes them to a live
ASP.NET Core MVC dashboard via SignalR.

## Architecture

```
Esp32Simulator ──MQTT (sensors/temperature)──► SensorApi ──► SQLite (sensor.db)
                                                    │
                                                    └─ SignalR /hubs/sensors
                                                           │  (ReadingReceived)
                                                           ▼
                                                    SensorDashboard (browser clients)
```

- **Esp32Simulator** — worker service that simulates an ESP32 (DHT-style) sensor.
  Publishes a JSON reading every second to the MQTT topic `sensors/temperature`.
- **SensorApi** — minimal API that subscribes to the MQTT topic, stores each reading
  in SQLite, exposes REST endpoints, and broadcasts every stored reading to SignalR
  clients in real time.
- **SensorDashboard** — ASP.NET Core MVC app that renders the monitoring dashboard.
  Loads recent history once from its own endpoint (reading the shared SQLite
  database), then receives all updates live over SignalR — no polling.

## Projects

| Project          | Type                     | Default URL                              |
| ---------------- | ------------------------ | ---------------------------------------- |
| SensorApi        | ASP.NET Core minimal API | `http://localhost:5258`                  |
| SensorDashboard  | ASP.NET Core MVC         | `http://localhost:5275`                  |
| Esp32Simulator   | Worker service           | n/a (publishes to MQTT)                  |

## Prerequisites

- .NET 10 SDK
- An MQTT broker listening on `localhost:1883` (e.g. Mosquitto)

## Running

Start all three (order matters: broker first, then API, then simulator/dashboard):

```powershell
dotnet run --project SensorApi        # ingestion + REST + SignalR hub
dotnet run --project Esp32Simulator   # simulated device
dotnet run --project SensorDashboard  # dashboard UI
```

Open the dashboard at <http://localhost:5275>.

## SensorApi

### REST endpoints

| Endpoint                     | Description                                             |
| ---------------------------- | ------------------------------------------------------- |
| `GET /`                      | Health check                                            |
| `GET /api/readings?limit=N`  | Latest N readings (1–1000, default 100), newest first   |
| `GET /api/readings/latest`   | Single most recent reading                              |

### SignalR hub

- URL: `/hubs/sensors`
- Event: `ReadingReceived` — payload is the stored `SensorReading`
  (camelCase JSON: `id`, `tempRead`, `humidity`, `readTime`, `unit`, `receivedAt`),
  broadcast to all connected clients immediately after each reading is saved.

### Configuration (`SensorApi/appsettings.json`)

```json
{
  "ConnectionStrings": { "SensorDatabase": "Data Source=sensor.db" },
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "Topic": "sensors/temperature",
    "ClientId": "sensor-api",
    "RetrySeconds": 5
  }
}
```

CORS policy `Dashboard` allows the MVC dashboard origins
(`http://localhost:5275`, `https://localhost:7214`) for the SignalR negotiate request.

## SensorDashboard

### Features

- Stat cards: current temperature (with min–max range), humidity, and window
  averages over the last 60 readings
- Dual-axis line chart (temperature °C left, humidity % right), rolling
  60-reading window
- Latest-readings table (10 rows)
- Status badge:
  - **Live** — hub connected, newest reading < 10 s old
  - **Stale** — hub connected, but no fresh reading for > 10 s
  - **Reconnecting… / Disconnected** — hub connection down; retries every 5 s
    and refills history after reconnecting so no readings are missed
  - **No data** — database has no readings

### How updates flow

1. On page load, the dashboard fetches history once from `GET /Home/Recent?limit=60`
   (served by the MVC app, which reads the shared `sensor.db` directly).
2. It then connects to the SensorApi SignalR hub; every `ReadingReceived` event is
   prepended to the in-memory window and the stats, chart, and table re-render.

### Configuration (`SensorDashboard/appsettings.json`)

```json
{
  "ConnectionStrings": { "SensorDatabase": "Data Source=../SensorApi/sensor.db" },
  "SensorApi": { "HubUrl": "http://localhost:5258/hubs/sensors" }
}
```

Chart.js 4.5.1 and the SignalR browser client are vendored under `wwwroot/lib/`,
so the dashboard works fully offline on a local network.

## MQTT payload

Published by the simulator once per second (`PublishIntervalSeconds`):

```json
{
  "TempRead": 21.53,
  "ReadTime": "2026-08-20T10:15:30.1234567Z",
  "Unit": "C",
  "Humidity": 34.21
}
```

## Building

```powershell
dotnet build iot_hobby.slnx
```
