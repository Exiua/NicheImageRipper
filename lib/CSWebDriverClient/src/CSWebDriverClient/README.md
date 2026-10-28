# CSWebDriverClient

A C# client for [CSWebDriver](https://github.com/Exiua/CSWebDriver), a separate browser-automation service used for
sites that Selenium can't handle on its own.

## Usage

Set `CSWebDriverUri` in `config.json` to the service's address. Parsers use it through the `Client` class:

```csharp
var client = new Client("http://localhost:5000");
var page = await client.GetPage(url, waitForXPath: "//div[@id='content']");
var requests = await client.GetNetworkUrls(url);   // URLs the page requested while loading
```

Each call returns a `BaseResponse` holding either the result or an `ErrorResponse`.
