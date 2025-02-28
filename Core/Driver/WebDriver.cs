using System.Drawing;
using Core.Configuration;
using OpenQA.Selenium.Firefox;
using Serilog;

namespace Core.Driver;

public class WebDriver : IDisposable
{
    private static Config Config => Config.Instance;
    private static string UserAgent => Config.UserAgent;
    
    public Dictionary<string, bool> SiteLoginStatus { get; set; } = new();
    public FirefoxDriver Driver { get; set; }
    public bool IsHeadless { get; }

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
    /// <param name="headless">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    public void RegenerateDriver(bool headless)
    {
        try
        {
            Driver.Quit();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to quit the WebDriver.");
        }
        
        Driver = CreateFirefoxDriver(headless);
        SiteLoginStatus.Clear();
    }
    
    /// <summary>
    ///     Create a new FirefoxDriver instance with the specified options.
    /// </summary>
    /// <param name="headless">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    /// <returns>A new FirefoxDriver instance.</returns>
    private static FirefoxDriver CreateFirefoxDriver(bool headless)
    {
        var options = InitializeOptions(headless);
        var driver = new FirefoxDriver(options);
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
    /// <returns>The initialized FirefoxOptions instance.</returns>
    private static FirefoxOptions InitializeOptions(bool headless)
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
        options.SetPreference("general.useragent.override", UserAgent);
        options.SetPreference("media.volume_scale", "0.0");
        
        return options;
    }

    public void Dispose()
    {
        Driver.Dispose();
        GC.SuppressFinalize(this);
    }
}