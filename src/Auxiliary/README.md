# Auxiliary

Developer tools for maintaining the codebase. Not shipped and not needed to use the app.

## Tools

| File                        | Does                                                                                      |
|-----------------------------|-------------------------------------------------------------------------------------------|
| `CheckSharedAssemblies.cs`  | Reports which assemblies Sdk's public API exposes, and diffs them against the shared list in `ModuleLoadContext.cs` |
| `CleanupProgram.cs`         | Removes duplicate `using`s and passes missing `CancellationToken`s through `await` calls  |
| `SupportedUrlsMigration.cs` | One-time migration of supported URLs onto each parser (already applied)                   |
| `RipInfoAsyncMigration.cs`  | One-time migration of `RipInfo` construction to the async API (already applied)            |

## How To Use

Uncomment the tool you want in `Program.cs`, then run it. Usage is at the top of each file, e.g.:

```bash
dotnet run --project src/Auxiliary -- check-shared-assemblies <path-to-NicheImageRipper.Sdk.dll>
dotnet run --project src/Auxiliary -- NicheImageRipper.sln --dry-run
```

Tools that edit code use Roslyn; run them with `--dry-run` first.
