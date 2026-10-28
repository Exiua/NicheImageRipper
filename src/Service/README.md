# Service

An ASP.NET Core web service that runs the ripper and exposes it over a REST API, so it can run on a server and be
controlled remotely (for example by the GUI's thin mode).

## How To Use

1. Create an API key with [ServiceAdminCli](../ServiceAdminCli). Keys are stored in `NicheImageRipper.db` in the
   data folder (`NIR_DATA_DIR`), or in the SQLite database `ConnectionStrings:DefaultConnection` points to. The
   database is created or migrated when the service or ServiceAdminCli starts.
2. Run the service and send the key in the `X-API-Key` header on every request.
3. Use HTTPS (or a TLS-terminating reverse proxy) if the service is reachable from other machines: keys, settings and
   site credentials travel in every request. The service logs a warning when it listens on plain HTTP beyond
   localhost.

```bash
dotnet run --project src/Service
```

| Endpoint          | Does                                                                     |
|-------------------|--------------------------------------------------------------------------|
| `/api/queue`      | Get, add to, or clear the queue; save it or load a saved one             |
| `/api/state`      | Start ripping, pause, resume, save, clear cache, check status            |
| `/api/history`    | Browse rip history                                                       |
| `/api/settings`   | Read and change settings, list supported sites                           |
| `/api/version`    | Current and latest version                                               |
| `/ws/events`      | WebSocket stream of live log and status events                           |

In Development the OpenAPI document is served at `/openapi/v1.json`. `CoreService.http` has ready-made requests.

Stopping the service (Ctrl+C or a service manager's stop) cancels a rip in progress and saves the queue, rip position
and settings. `PATCH /api/settings` saves settings right away.

### Live events and logs

`/ws/events` sends JSON envelopes (`WsEnvelope`): log events, queue changes and rip progress. Each client chooses the
lowest log level it receives:

- when connecting, with `?logLevel=Warning` (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`);
- at any time, by sending `{"Type":"SetLogLevel","LogLevel":"Debug"}`.

The service answers each with a `LogLevelChanged` event holding the level it applied. That can be higher than asked
for: clients can't go below `ServiceLogging:WebSocketMinimumLevel`. The GUI's thin mode asks for Debug while "Show
debug logs" is on, and Information otherwise.

### Logging settings (`appsettings.json`)

| Setting                                  | Default       | Does                                                    |
|------------------------------------------|---------------|---------------------------------------------------------|
| `ServiceLogging:MinimumLevel`            | `Information` | Lowest level written to the console and log file        |
| `ServiceLogging:WebSocketMinimumLevel`   | `Debug`       | Lowest level a WebSocket client can subscribe to        |
| `ServiceLogging:WebSocketDefaultLevel`   | `Information` | Level for clients that don't ask for one                |

## Development

- `Controllers/` are the endpoints, `Singletons/NicheImageRipperSingleton.cs` wraps the single shared [Core](../Core)
  ripper instance, `Handlers/` holds the API key authentication.
- `Singletons/WebSocketBroadcaster.cs` serves `/ws/events`. Each client has a queue drained by one send loop (a
  WebSocket allows only one send at a time); `Serilog/WebSocketLogSink.cs` feeds it log events.
  `Services/RipperLifetimeService.cs` forwards ripper events to it and saves the ripper on shutdown.
- Types clients need (requests, responses, WebSocket messages) live in [Service.Contracts](../Service.Contracts), so
  clients like the GUI don't reference this project.
- The Service doesn't reference [SiteModules](../SiteModules); it loads `SiteModules.dll` from its output folder,
  so build SiteModules alongside it.
- EF Core migrations are in `Migrations/`. Add one with `dotnet ef migrations add <Name> --project src/Service`.
- Config, history and logs use the same data folder as the other apps (`NIR_DATA_DIR`).
