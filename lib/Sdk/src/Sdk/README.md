# Sdk

The shared contract between [Core](../../../../src/Core) and site parsers. Everything a parser needs to build against
lives here, so [SiteModules](../../../../src/SiteModules) and drop-in modules only reference this, not Core.

## What's In It

| Folder              | Does                                                                                   |
|---------------------|----------------------------------------------------------------------------------------|
| `SiteParsing/`      | `HtmlParser` base class, `IHtmlParser`, and optional parser interfaces                 |
| `DataStructures/`   | `RipInfo` (what a parser returns) and the file link types                              |
| `FileDownloading/`  | Interfaces for custom download strategies, header modifiers and validators             |
| `Configuration/`    | `Config` / `GeneralConfig` (`config.json`), per-site settings and credentials          |
| `Features/`         | Checks for optional external tools (ffmpeg, yt-dlp, MegaCMD, FlareSolverr, ...)        |
| `Driver/`           | The Selenium Firefox `WebDriver` wrapper                                               |
| `Utility/`          | URL helpers, `AppData` (the data folder, overridable with `NIR_DATA_DIR`), and more   |
| `Exceptions/`       | Exceptions parsers throw to signal specific failures (API key required, missing cookie, URL expired, ...)|

## Development

Types that cross the Sdk boundary must load once, shared by the app and all modules. If a public Sdk API starts
exposing a type from a new package, add that assembly to `SharedAssemblyNames` in Core's `ModuleLoadContext.cs`;
the `CheckSharedAssemblies` tool in [Auxiliary](../../../../src/Auxiliary) reports any that are missing.

Tests are in [Sdk.Tests](../../test/Sdk.Tests).
