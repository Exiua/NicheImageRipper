using Core.Configuration;
using OpenQA.Selenium.Firefox;
using Serilog;

namespace Core.History;

public class WebDriver : IDisposable
{
    private static Config Config => Config.Instance;
    private static string UserAgent => Config.UserAgent;
    
    public Dictionary<string, bool> SiteLoginStatus { get; set; } = new();
    public FirefoxDriver Driver { get; set; }

    public string CurrentUrl
    {
        get => Driver.Url;
        set => Driver.Url = value;
    }

    public WebDriver(bool debug)
    {
        Driver = CreateFirefoxDriver(debug);
    }
    
    /// <summary>
    ///     Regenerate the WebDriver instance. This is useful when the WebDriver is no longer responsive.
    /// </summary>
    /// <param name="debug">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    public void RegenerateDriver(bool debug)
    {
        try
        {
            Driver.Quit();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to quit the WebDriver.");
        }
        
        Driver = CreateFirefoxDriver(debug);
        SiteLoginStatus.Clear();
    }
    
    /// <summary>
    ///     Create a new FirefoxDriver instance with the specified options.
    /// </summary>
    /// <param name="debug">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    /// <returns>A new FirefoxDriver instance.</returns>
    private static FirefoxDriver CreateFirefoxDriver(bool debug)
    {
        var options = InitializeOptions(debug);
        return new FirefoxDriver(options);
    }
    
    /// <summary>
    ///     Initialize the FirefoxOptions for the WebDriver. Enables headless mode if debug is false. Enables web socket
    ///     URL to allow for BiDi communication. Sets the user agent to the one specified in the configuration file.
    ///     Disables audio by setting the volume scale to 0.0.
    /// </summary>
    /// <param name="debug">Whether to run the WebDriver in headless mode. Mainly for debugging purposes.</param>
    /// <returns>The initialized FirefoxOptions instance.</returns>
    private static FirefoxOptions InitializeOptions(bool debug)
    {
        var options = new FirefoxOptions
        {
            UseWebSocketUrl = true
        };
        
        if (!debug)
        {
            options.AddArgument("--headless");
        }

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