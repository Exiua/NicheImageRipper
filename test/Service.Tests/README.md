# Service.Tests

xUnit tests for [Service](../../src/Service): API key creation and authentication, the history and state
controllers, settings validation, and the WebSocket broadcaster.

```bash
dotnet test --project test/Service.Tests
```

`NotImplementedRipperSingleton` is a stand-in ripper for controller tests; override only what a test needs.
`FakeWebSocket` is an in-memory socket for broadcaster tests; like the real one, it fails overlapping sends.
