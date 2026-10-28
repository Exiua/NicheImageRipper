# ServiceAdminCli

A small command-line tool for managing the API keys that [Service](../Service) accepts.

## How To Use

It reads the same `ConnectionStrings:DefaultConnection` setting as the Service, so point both at the same database.

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
