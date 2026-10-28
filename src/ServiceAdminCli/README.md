# ServiceAdminCli

A small command-line tool for managing the API keys that [Service](../Service) accepts.

## How To Use

Like the Service, it uses `NicheImageRipper.db` in the data folder (`NIR_DATA_DIR`, by default the executable's
folder) unless `ConnectionStrings:DefaultConnection` is set. Point both at the same database: run them with the same
`NIR_DATA_DIR`, or set the same connection string for both. The database is created or migrated on start.

```bash
ServiceAdminCli gen <name>       # create a key; it's shown once, so store it
ServiceAdminCli list             # list keys and whether they're revoked
ServiceAdminCli revoke <id>      # revoke a key by its id
```

## Development

```bash
dotnet run --project src/ServiceAdminCli -- gen my-key
```

Everything is in `Program.cs`; key handling itself lives in the Service's `ApiKeyService`.
