# FlareSolverrIntegration

A C# client for [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr), a proxy that gets past Cloudflare's
browser checks. The ripper uses it for sites behind Cloudflare.

## Usage

Run FlareSolverr and set `FlareSolverrUri` in `config.json` (e.g. `http://localhost:8191/v1`). In code:

```csharp
var client = new FlareSolverrClient("http://localhost:8191/v1");
await client.CreateSession("my-session");
var page = await client.GetRequest(new GetRequestPayload { Url = url, Session = "my-session" });
```

It covers FlareSolverr's session commands (`CreateSession`, `ListSessions`, `DeleteSession`) and requests
(`GetRequest`, `PostRequest`). `CloseFlareSolverrSession` in the config controls whether the ripper closes its session
when done.
