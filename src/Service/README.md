# Service

An ASP.NET Core web service that runs the ripper and exposes it over a REST API, so it can run on a server and be
controlled remotely (for example by the GUI's thin mode).

## How To Use

1. Set `ConnectionStrings:DefaultConnection` in `appsettings.json` to a SQLite database, e.g.
   `"Data Source=NicheImageRipper.db"`. This holds the API keys.
2. Create an API key with [ServiceAdminCli](../ServiceAdminCli).
3. Run the service and send the key in the `X-API-Key` header on every request.

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

## Development

- `Controllers/` are the endpoints, `Singletons/NicheImageRipperSingleton.cs` wraps the single shared [Core](../Core)
  ripper instance, `Handlers/` holds the API key authentication.
- The Service doesn't reference [SiteModules](../SiteModules); it loads `SiteModules.dll` from its output folder,
  so build SiteModules alongside it.
- EF Core migrations are in `Migrations/`. Add one with `dotnet ef migrations add <Name> --project src/Service`.
- Config, history and logs use the same data folder as the other apps (`NIR_DATA_DIR`).
