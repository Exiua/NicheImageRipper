using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace CloudflareBypass;

public class Cloudscrapper
{
    private const string DriverHeader =
        "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0";
    
    private WebDriver _driver;

    public Cloudscrapper()
    {
        var options = new FirefoxOptions();
        options.AddArgument(DriverHeader);
        _driver = new FirefoxDriver(options);
    }
}