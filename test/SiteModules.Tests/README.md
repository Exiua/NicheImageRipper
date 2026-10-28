# SiteModules.Tests

xUnit tests for [SiteModules](../../src/SiteModules): URL normalization, site link rules, booru search expansion,
MangaDex language handling, and Google Drive.

```bash
dotnet test --project test/SiteModules.Tests
```

The Google Drive tests hit the real API and are skipped unless `NIR_GDRIVE_TEST_FOLDER_URL` is set to a Drive folder
URL and `Workspace/client_secrets.json` exists.
