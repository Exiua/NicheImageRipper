using OpenQA.Selenium;
using Serilog;

namespace Sdk.Common.ExtensionMethods;

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

    public static IWebElement? TryFindElement(this IWebElement webElement, By by)
    {
        try
        {
            return webElement.FindElement(by);
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Alias for <see cref="IWebDriver.Navigate().Refresh()"/>.
    ///     This method refreshes the current page in the browser.
    /// </summary>
    /// <param name="driver">WebDriver instance</param>
    /// <param name="hardRefresh">Whether to perform a hard refresh (only works on Firefox)</param>
    public static void Refresh(this IWebDriver driver, bool hardRefresh = false)
    {
        if (hardRefresh)
        {
            ((WebDriver)driver).ExecuteScript("location.reload(true);");
        }
        else
        {
            driver.Navigate().Refresh();
        }
    }

    public static ICookieJar GetCookieJar(this IWebDriver driver)
    {
        return driver.Manage().Cookies;
    }

    public static void ClearCookies(this IWebDriver driver)
    {
        driver.GetCookieJar().DeleteAllCookies();
    }

    /// <summary>
    ///     Adds a cookie to the current session. Intended for adding a single cookie. If you need to add multiple
    /// cookies, use the cookie jar directly.
    /// </summary>
    /// <param name="driver">WebDriver instance</param>
    /// <param name="cookieName">Name of the cookie</param>
    /// <param name="cookieValue">Value of the cookie</param>
    public static void AddCookie(this IWebDriver driver, string cookieName, string cookieValue)
    {
        driver.GetCookieJar().AddCookie(new Cookie(cookieName, cookieValue));
    }

    /// <summary>
    ///     Adds a cookie to the current session. Intended for adding a single cookie. If you need to add multiple
    /// cookies, use the cookie jar directly.
    /// </summary>
    /// <param name="driver">WebDriver instance</param>
    /// <param name="cookie">Cookie to add</param>
    public static void AddCookie(this IWebDriver driver, Cookie cookie)
    {
        driver.GetCookieJar().AddCookie(cookie);
    }

    public static void AddCookie(this ICookieJar cookieJar, string cookieName, string cookieValue)
    {
        cookieJar.AddCookie(new Cookie(cookieName, cookieValue));
    }

    public static void SetCookie(this IWebDriver driver, string cookieName, string cookieValue,
                                 string? domain = null, string? path = null, DateTime? expiry = null,
                                 bool secure = false, bool httpOnly = false, string? sameSite = null)
    {
        driver.GetCookieJar().SetCookie(cookieName, cookieValue, domain, path, expiry, secure, httpOnly, sameSite);
    }

    public static void SetCookie(this ICookieJar cookieJar, string cookieName, string newCookieValue,
                                 string? domain = null, string? path = null, DateTime? expiry = null,
                                 bool secure = false, bool httpOnly = false, string? sameSite = null)
    {
        var newCookie = new Cookie(cookieName, newCookieValue, domain, path, expiry, secure, httpOnly, sameSite);
        cookieJar.SetCookie(newCookie);
    }

    public static void SetCookie(this ICookieJar cookieJar, Cookie cookie)
    {
        var existingCookie = cookieJar.GetCookieNamed(cookie.Name);
        if (existingCookie is not null)
        {
            Log.Debug("Replacing existing cookie: {CookieName}", cookie.Name);
            cookieJar.DeleteCookie(existingCookie);
            cookie = new Cookie(cookie.Name, cookie.Value, existingCookie.Domain, existingCookie.Path,
                existingCookie.Expiry, existingCookie.Secure,
                existingCookie.IsHttpOnly, existingCookie.SameSite);
        }
        else
        {
            Log.Debug("Adding new cookie: {CookieName}", cookie.Name);
        }

        cookieJar.AddCookie(cookie);
    }

    public static void SetOrUpdateCookie(this IWebDriver driver, string cookieName, string cookieValue)
    {
        var cookieJar = driver.GetCookieJar();
        var existingCookie = cookieJar.GetCookieNamed(cookieName);
        if (existingCookie is not null)
        {
            var existingValue = existingCookie.Value;
            if (existingValue != cookieValue)
            {
                Log.Debug("Replacing existing cookie: {CookieName}", cookieName);
                cookieJar.SetCookie(cookieName, cookieValue);
            }
            else
            {
                Log.Debug("Cookie already has correct value");
            }
        }
        else
        {
            cookieJar.AddCookie(cookieName, cookieValue);
        }
    }

    public static string GetSrc(this IWebElement element)
    {
        var src = element.GetDomAttribute("src");
        if (src is null)
        {
            throw new NoSuchElementException("Element does not have a src attribute.");
        }

        return src;
    }

    public static string GetHref(this IWebElement element)
    {
        var href = element.GetDomAttribute("href");
        if (href is null)
        {
            throw new NoSuchElementException("Element does not have a href attribute.");
        }

        return href;
    }

    public static void ScrollElementIntoView(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
    }

    public static void Click(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
    }

    public static void RemoveElement(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].remove();", element);
    }

    public static long GetScrollHeight(this IWebDriver driver)
    {
        var height = ((IJavaScriptExecutor)driver).ExecuteScript("return window.pageYOffset;");
        return height switch
        {
            long l => l,
            double d => (long)d,
            _ => throw new InvalidCastException()
        };
    }

    public static Cookie ToSeleniumCookie(this FlareSolverrIntegration.Responses.Cookie cookie)
    {
        var expiration = DateTimeOffset.FromUnixTimeSeconds(cookie.Expiry).UtcDateTime;
        var seleniumCookie = new Cookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, expiration,
            cookie.Secure,
            cookie.HttpOnly, cookie.SameSite);
        return seleniumCookie;
    }

    public static bool DoElementsOverlap(this IWebDriver driver, IWebElement element1, IWebElement element2)
    {
        var rect1 = element1.Location;
        var size1 = element1.Size;
        var rect2 = element2.Location;
        var size2 = element2.Size;

        return !(rect1.X > rect2.X + size2.Width ||
                 rect1.X + size1.Width < rect2.X ||
                 rect1.Y > rect2.Y + size2.Height ||
                 rect1.Y + size1.Height < rect2.Y);
    }

    public static void SetLocalStorageItem(this IWebDriver driver, string key, string value)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript("localStorage.setItem(arguments[0], arguments[1]);", key, value);
    }
}