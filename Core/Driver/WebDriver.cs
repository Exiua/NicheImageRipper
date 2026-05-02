using Core.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Serilog;

namespace Core.Driver;

public class WebDriver : IDisposable
{
    private static GeneralConfig Config => Configuration.Config.Instance;
    private static string UserAgent => Config.UserAgent;
    private static ILogger Logger { get; } = Log.ForContext<WebDriver>();
    
    public Dictionary<string, bool> SiteLoginStatus { get; set; } = new();
    public FirefoxDriver Driver { get; set; }
    public bool IsHeadless { get; }
    
    private bool _disposed;

    public string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    public WebDriver(bool headless)
    {
        Driver = CreateFirefoxDriver(headless);
        IsHeadless = headless;
    }

    /// <summary>
    ///     Regenerate the WebDriver instance. This is useful when the WebDriver is no longer responsive.
    /// </summary>
    /// <param name="userAgent">Optional user agent string to override the default.</param>
    public void RegenerateDriver(string? userAgent = null)
    {
        try
        {
            Driver.Quit();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to quit the WebDriver.");
        }
        
        Driver = CreateFirefoxDriver(IsHeadless, userAgent);
        SiteLoginStatus.Clear();
    }

    /// <summary>
    ///     Create a new FirefoxDriver instance with the specified options.
    /// </summary>
    /// <param name="headless">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    /// <param name="userAgent">Optional user agent string to override the default.</param>
    /// <returns>A new FirefoxDriver instance.</returns>
    private static FirefoxDriver CreateFirefoxDriver(bool headless, string? userAgent = null)
    {
        const int maxRetry = 4;
        var options = InitializeOptions(headless);
        // Safety: Driver will either be created or an exception will be thrown.
        FirefoxDriver driver = null!;
        for (var retry = 0; retry < maxRetry; retry++)
        {
            try
            {
                driver = new FirefoxDriver(options);
                break;
            }
            catch (UnknownErrorException e)
            {
                if (retry == maxRetry - 1)
                {
                    Logger.Error(e, "Failed to create FirefoxDriver after 4 attempts.");
                    throw new Exception("Failed to create FirefoxDriver after 4 attempts.", e);
                }

                Logger.Warning(e, "Failed to create FirefoxDriver. Retrying...");
            }
        }
        //driver.Manage().Window.Size = new Size(2560, 1440);
        driver.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => false});");
        return driver;
    }

    /// <summary>
    ///     Initialize the FirefoxOptions for the WebDriver. Enables headless mode if debug is false. Enables web socket
    ///     URL to allow for BiDi communication. Sets the user agent to the one specified in the configuration file.
    ///     Disables audio by setting the volume scale to 0.0.
    /// </summary>
    /// <param name="headless">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    /// <param name="userAgent">Optional user agent string to override the default.</param>
    /// <returns>The initialized FirefoxOptions instance.</returns>
    private static FirefoxOptions InitializeOptions(bool headless, string? userAgent = null)
    {
        var options = new FirefoxOptions
        {
            UseWebSocketUrl = true
        };
        
        if (headless)
        {
            options.AddArgument("--headless");
        }

        options.AddArgument("--width=2560");
        options.AddArgument("--height=1440");
        
        options.SetPreference("webgl.disabled", false);
        options.SetPreference("layers.acceleration.disabled", false);
        options.SetPreference("dom.serviceWorkers.enabled", true);
        options.SetPreference("dom.indexedDB.enabled", true);
        options.SetPreference("general.useragent.override", userAgent ?? UserAgent);
        options.SetPreference("media.volume_scale", "0.0");
        
        return options;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        Driver.Dispose();
        GC.SuppressFinalize(this);
    }
    
    ~WebDriver()
    {
        Dispose();
    }
}