# Core.Tests

xUnit tests for [Core](../../src/Core): history and its filters, gallery update planning and file renaming, URL
normalization, URL list files, and HTML parsing helpers.

```bash
dotnet test --project test/Core.Tests
```

Tests that touch app data (history database, unfinished rips) join the `DataFolder` collection, which points the data
folder at a temp directory and runs them one at a time.
