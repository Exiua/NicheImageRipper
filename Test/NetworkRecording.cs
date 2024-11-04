using System.Collections.ObjectModel;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools;
using OpenQA.Selenium.Firefox;
using Network = OpenQA.Selenium.DevTools.V129.Network;
using DevToolsSessionDomains = OpenQA.Selenium.DevTools.V129.DevToolsSessionDomains;

namespace Test;

public class NetworkRecording
{
    public static async Task TestFireFox()
    {
        const string url = "https://x.com/LoveAnimeGirlss/status/1803131613255004655";
        var options = new FirefoxOptions
        {
            // EnableDevToolsProtocol = true,
            UseWebSocketUrl = true,
        };
        using var driver = new FirefoxDriver(options: options);
        var bidi = await driver.AsBiDiAsync();
        await bidi.Network.OnResponseCompletedAsync((e) =>
        {
            var url = e.Response.Url;
            if (!url?.Contains(".m3u8") ?? true)
            {
                return;
            }
            
            Console.WriteLine("New Work Response received");
            Console.WriteLine($"Response Status: {e.Response.Status}");
            Console.WriteLine($"Status Text: {e.Response.StatusText}");
            Console.WriteLine($"Response MimeType: {e.Response.MimeType}");
            Console.WriteLine($"Response Url: {e.Response.Url}");
        });

        await driver.Navigate().GoToUrlAsync(url);
        var cookies = driver.Manage().Cookies;
        cookies.AddCookie(new Cookie("auth_token", ""));
        await driver.Navigate().GoToUrlAsync(url);

        Console.WriteLine("Press any key to exit");
        _ = Console.ReadLine();
        Console.WriteLine("finished");
    }
    
    public static async Task TestChrome()
    {
        const string url = "https://x.com/Sharlean_Tails/status/1817205020628287958";
        using var driver = new ChromeDriver();
        IDevTools devTools = driver;
        IDevToolsSession session = devTools.GetDevToolsSession();
        var domains = session.GetVersionSpecificDomains<DevToolsSessionDomains>();
        domains.Network.ResponseReceived += ResponseReceivedHandler;
        Task task = domains.Network.Enable(new Network.EnableCommandSettings());
        task.Wait();

        await driver.Navigate().GoToUrlAsync(url);
        var cookies = driver.Manage().Cookies;
        cookies.AddCookie(new Cookie("auth_token", "a1f647212bf77c611c2cbed940f8d7affd8e2ee1"));
        await driver.Navigate().GoToUrlAsync(url);

        Console.WriteLine("Press any key to exit");
        _ = Console.ReadLine();
        Console.WriteLine("finished");
        return;

        void ResponseReceivedHandler(object? sender,Network.ResponseReceivedEventArgs e)
        {
            var url = e.Response.Url;
            if (!url.Contains(".m3u8"))
            {
                return;
            }
            
            Console.WriteLine("New Work Response received");
            Console.WriteLine($"Response Status: {e.Response.Status}");
            Console.WriteLine($"Status Text: {e.Response.StatusText}");
            Console.WriteLine($"Response MimeType: {e.Response.MimeType}");
            Console.WriteLine($"Response Url: {e.Response.Url}");
        }
    }
}