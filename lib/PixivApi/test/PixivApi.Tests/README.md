# PixivApi.Tests

xUnit tests for [PixivApi](../../src/PixivApi). They call the real Pixiv API, so they need a Pixiv refresh token:
set `RefreshToken` in `ApiTest.cs` before running, and don't commit it.

```bash
dotnet test --project lib/PixivApi/test/PixivApi.Tests
```
