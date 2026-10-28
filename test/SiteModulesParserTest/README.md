# SiteModulesParserTest

A developer tool for debugging site parsers. It runs one parser against one URL and reports what it found, without
downloading anything.

## How To Use

```bash
dotnet run --project test/SiteModulesParserTest -- <url> [--debug] [--print-site]
```

- `--debug` / `-d`: show the browser instead of running headless, and wait for a key press before closing it
- `--print-site` / `-p`: save the page source to `test.html`

Found URLs are written to `test.json`; on failure it also saves `test.html` and a screenshot. These, and the
`config.json` it reads, are in the data folder (next to the executable, or `NIR_DATA_DIR`). Point `NIR_DATA_DIR` at
your normal data folder to use your real logins and cookies.
