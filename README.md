# NicheImageRipper
[![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Exiua/NicheImageRipper/blob/main/LICENSE) ![GitHub Release](https://img.shields.io/github/v/release/Exiua/NicheImageRipper?include_prereleases&label=latest) ![GitHub Release](https://img.shields.io/github/v/release/Exiua/NicheImageRipper?label=stable)
 ![GitHub all releases](https://img.shields.io/github/downloads/Exiua/NicheImageRipper/total)

## About

NicheImageRipper is an image ripper written in C# to download albums, galleries and videos from 200+ sites.
Paste one or more URLs, and it queues them, finds every image/video on the page, and downloads them into a folder per
album. It keeps a history of what you've ripped, can re-rip a gallery to fetch only what's new, and saves unfinished
queues so you can pick up where you left off.

It comes as a desktop app (Windows, Linux, macOS), a command-line app, and a web service with a REST API.

## How To Use

1. Download `NicheImageRipper-<version>-<platform>.zip` from the
   [latest release](https://github.com/Exiua/NicheImageRipper/releases) and extract it anywhere.
2. Install [Firefox](https://www.mozilla.org/firefox/) and the [external dependencies](#external-dependencies).
3. Run `NicheImageRipper` (`NicheImageRipper.exe` on Windows), paste URLs into the box and press **Rip**.

Downloads go to `./Rips/` by default; change it in the **Settings** tab. Config (`config.json`), history, logs and saved
queues are kept next to the executable. Set the `NIR_DATA_DIR` environment variable to keep them somewhere else.

Some sites need a login, API key or cookies. Add them in **Settings → Sites**, or under `Sites` in `config.json`.

## Dependencies

### External Dependencies

The program requires [Firefox](https://www.mozilla.org/firefox/), [ffmpeg](https://www.ffmpeg.org/) and
[yt-dlp](https://github.com/yt-dlp/yt-dlp) to be installed and on your `PATH`.

The program also (optionally) uses [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr) and
[CSWebDriver](https://github.com/Exiua/CSWebDriver) for the sites that need them. Without them, only those sites are affected.

Mega links are downloaded with the [MEGA SDK](https://github.com/meganz/sdk), which needs an app key (generate one at
https://mega.nz/sdk) set as `key` in the `mega` site config. A `login` there is optional and uses that account's
transfer quota.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build NicheImageRipper.sln
dotnet run --project src/Gui      # desktop app
dotnet run --project src/Tui      # command-line app
dotnet test                       # run tests
```

The [curl-impersonate](https://github.com/lexiforest/curl-impersonate) native library is not in the repo; see
[ImpersonateClient](lib/ImpersonateClient/src/ImpersonateClient/README.md). Neither is the MEGA SDK native library; see
[MegaSdk](lib/MegaSdk/src/MegaSdk/README.md).

## Project Layout

| Project                                                         | What it is                                                  |
|-----------------------------------------------------------------|-------------------------------------------------------------|
| [Gui](src/Gui)                                                  | Avalonia desktop app, the main way to use the ripper        |
| [Tui](src/Tui)                                                  | Command-line app and interactive REPL                       |
| [Service](src/Service)                                          | ASP.NET Core web service exposing the ripper over REST      |
| [Service.Contracts](src/Service.Contracts)                      | Request, response and WebSocket types shared with clients   |
| [ServiceAdminCli](src/ServiceAdminCli)                          | Manages API keys for the service                            |
| [Core](src/Core)                                                | The engine: queue, downloading, history, module loading     |
| [SiteModules](src/SiteModules)                                  | The built-in site parsers (one per supported site)          |
| [Auxiliary](src/Auxiliary)                                      | One-off dev tools (code migrations, checks)                 |
| [Sdk](lib/Sdk/src/Sdk)                                          | Shared types and base classes for writing site parsers      |
| [ImpersonateClient](lib/ImpersonateClient/src/ImpersonateClient) | HTTP client that mimics real browser TLS fingerprints       |
| [CSWebDriverClient](lib/CSWebDriverClient/src/CSWebDriverClient) | Client for the CSWebDriver service                          |
| [FlareSolverrIntegration](lib/FlareSolverrIntegration/src/FlareSolverrIntegration) | Client for FlareSolverr                  |
| [IwaraApiClient](lib/IwaraApiClient/src/IwaraApiClient), [MangaDexLibrary](lib/MangaDexLibrary/src/MangaDexLibrary), [NHentaiApi](lib/NHentaiApi/src/NHentaiApi), [PatreonApiClient](lib/PatreonApiClient/src/PatreonApiClient), [PixivApi](lib/PixivApi/src/PixivApi), [SteamApiClient](lib/SteamApiClient/src/SteamApiClient) | API clients for sites that are ripped through their API |
| [test/](test)                                                   | Unit tests and the parser debugging tool                    |

## Sites Currently Supported:

- [imhentai.com](https://imhentai.com/)
- [hentai.cafe](https://hentai.cafe)
- [hotgirl.asia](https://hotgirl.asia/)
- [cup-e.club](https://www.cup-e.club/)
- [girlsreleased.com](https://girlsreleased.com/)
- [bustybloom.com](https://bustybloom.com/)
- [morazzia.com](https://morazzia.com/)
- [novojoy.com](https://novojoy.com/)
- [hqbabes.com](https://www.hqbabes.com/)
- [silkengirl.com](https://www.silkengirl.com/)
- [babesandgirls.com](https://www.babesandgirls.com/)
- [babeimpact.com](https://www.babeimpact.com/)
- [100bucksbabes.com](https://www.100bucksbabes.com/)
- [sexykittenporn.com](https://www.sexykittenporn.com/)
- [babesbang.com](https://www.babesbang.com/)
- [exgirlfriendmarket.com](https://www.exgirlfriendmarket.com/)
- [novoporn.com](https://www.novoporn.com/)
- [(more)](https://github.com/Exiua/NicheImageRipper/wiki/Supported-Sites)
