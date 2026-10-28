# MangaDexLibrary

A C# client for the [MangaDex](https://mangadex.org/) API, used by the MangaDex parser to rip manga chapters.

## What's Implemented

- `Manga`: manga metadata and its volume/chapter list
- `Chapter`: chapter details
- `AtHome`: the image server URLs for a chapter's pages

```csharp
using var client = new MangaDexClient();
```

Each endpoint group is a property on `MangaDexClient`; pass your own `HttpClient` to the constructor if needed.
