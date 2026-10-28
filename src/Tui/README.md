# Tui

The NicheImageRipper command-line app. Run it with no arguments for an interactive prompt (REPL), or pass a command
to run it once and exit.

## How To Use

```bash
Tui                        # interactive prompt
Tui rip <url> [url...]     # rip the given URLs and exit
Tui rip <url> -i           # rip, then stay in the interactive prompt
```

Type a URL at the prompt to queue it, then `rip` to start. Common commands:

| Command                     | Does                                                 |
|-----------------------------|------------------------------------------------------|
| `rip [urls...]`             | Queue URLs (if given) and start ripping              |
| `queue`, `peek`, `tail`     | Show the queue, or its first/last URL                |
| `skip [index]`              | Remove a URL from the queue                          |
| `load [file]`, `save`       | Load URLs from a file, or save the current state     |
| `booru <tags...>`           | Queue a tag search on every supported booru          |
| `history`                   | Show recently ripped URLs                            |
| `config`, `retries`, `delay` | View or change settings                             |
| `clear cache`, `clear queue` | Clear the partial-save cache or the queue           |
| `help`, `quit`              | Show all commands, or save and exit                  |

It uses the same `config.json` and data folder as the GUI.

## Development

```bash
dotnet run --project src/Tui -- <command>
```

Commands are defined once with System.CommandLine in `NicheImageRipperCli.BuildRootCommand()` and shared by both
modes. To add one, write a `Build...Command()` method and register it there. To debug a single site parser, use
[SiteModulesParserTest](../../test/SiteModulesParserTest) instead.
