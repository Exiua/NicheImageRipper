# Gui

The NicheImageRipper desktop app, built with [Avalonia](https://avaloniaui.net/) so it runs on Windows, Linux and
macOS. This is what ships in the releases as `NicheImageRipper(.exe)`.

## How To Use

Paste one or more URLs into the box at the top and press **Rip** (or Enter). The tabs below show:

- **Queue**: URLs waiting to be ripped. Remove, clear, or save the queue to a file and load it later.
- **History**: everything you've ripped, searchable by name, URL or date. **Re-rip** fetches a gallery again
  (only new files, depending on the gallery update mode in the settings).
- **Logs**: what the ripper is doing right now.
- **Settings**: save folder, filename scheme, retries, threads, theme, per-site options, and more.

Settings are saved to `config.json` in the data folder (next to the executable, or `NIR_DATA_DIR`).

## Development

```bash
dotnet run --project src/Gui
```

- `Views/` holds the windows (`.axaml`), `ViewModels/` the logic behind them (ReactiveUI).
- The GUI has two modes, wired up in `App.axaml.cs`:
  - **Full** (default): runs the ripper in-process through [Core](../Core). Code lives in the `Full` folders.
  - **Thin** (`App.Thin`, experimental): connects to a running [Service](../Service) over HTTP and WebSocket
    instead. Code lives in the `Thin` folders.
- [SiteModules](../SiteModules) is copied to the output so the built-in parsers are always available.
- Release builds are single-file publishes (see `.github/workflows/build.yml`); Selenium's `selenium-manager` and
  the native `runtimes/` folder stay on disk next to the executable.
