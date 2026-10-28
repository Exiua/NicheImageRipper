# NHentaiApi

A C# client for the [nhentai](https://nhentai.net/) API, intended for the nhentai parser.

## What's Implemented

- Fetching a gallery's details by id
- Getting a gallery's download URL

```csharp
using var client = new NHentaiClient(apiKey);
var gallery = await client.GetGalleryAsync(123456);
```

Requires an nhentai API key. The nhentai parser currently scrapes the site instead, so this client isn't used yet.
