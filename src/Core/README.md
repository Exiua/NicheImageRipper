# Core

The ripping engine shared by every app ([Gui](../Gui), [Tui](../Tui), [Service](../Service)). It has no UI; front ends
subclass `NicheImageRipper` and call into it. Its `<Version>` is the release version.

## What's In It

| Folder             | Does                                                                                     |
|--------------------|------------------------------------------------------------------------------------------|
| `NicheImageRipper.cs` | The base ripper: queue, rip loop, saving/loading state, re-rip handling               |
| `ModuleLoading/`   | Loads site parsers: the built-in `SiteModules.dll`, then any drop-in modules             |
| `SiteParsing/`     | Picks the parser for a URL and runs it                                                   |
| `FileDownloading/` | Downloads files (HTTP, M3U8, MPD, external tools), extracts archives, validates results  |
| `GalleryUpdates/`  | Works out what's new when re-ripping a gallery, and renames files to match               |
| `History/`         | The SQLite rip history and its filters                                                   |
| `PartialSaves/`, `Cache/` | Resumes interrupted rips                                                          |
| `Driver/`          | The pool of Selenium Firefox drivers                                                     |

## Drop-in Modules

Extra site parsers can be added without rebuilding: put each module's DLLs in its own folder under `modules/` next to
the executable. Folders load in alphabetical order, or in the order listed in `modules/loadOrder.json` (a JSON array
of folder names). Modules build against [Sdk](../../lib/Sdk/src/Sdk), which is shared with the app rather than loaded
per module.

## Development

Startup goes through `CoreBootstrap.EnsureInitialized()`, which loads modules and sets up the parser factory; call it
before touching parsers directly. Tests are in [Core.Tests](../../test/Core.Tests).
