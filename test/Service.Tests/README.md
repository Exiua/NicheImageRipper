# Service.Tests

xUnit tests for [Service](../../src/Service): API key creation and authentication, credentials, and the history
controller.

```bash
dotnet test --project test/Service.Tests
```

`NotImplementedRipperSingleton` is a stand-in ripper for controller tests; override only what a test needs.
