using OpenQA.Selenium;

namespace Core.ExtensionMethods;

public static class SeleniumExtensionMethods
{
    public static IWebElement? TryFindElement(this IWebDriver driver, By by)
    {
        try
        {
            return driver.FindElement(by);
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }
    
    public static void Reload(this IWebDriver driver)
    {
        driver.Navigate().Refresh();
    }
    
    public static ICookieJar GetCookieJar(this IWebDriver driver)
    {
        return driver.Manage().Cookies;
    }
}