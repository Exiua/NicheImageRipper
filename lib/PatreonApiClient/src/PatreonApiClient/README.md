# PatreonApiClient

A C# client for [Patreon](https://www.patreon.com/)'s web API, used by the Patreon parser to rip a creator's posts.

## What's Implemented

- Paging through all posts of a creator (`GetPosts`)

```csharp
using var client = new PatreonClient(sessionId);
await foreach (var page in client.GetPosts("https://www.patreon.com/c/creator"))
{
    // ...
}
```

Authenticates with your Patreon `session_id` cookie, so it sees whatever your account can access. The ripper uses
the cookie set for the `patreon` site (**Settings → Sites** in the GUI).
