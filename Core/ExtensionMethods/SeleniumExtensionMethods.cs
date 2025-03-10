﻿using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

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
    
    public static void Refresh(this IWebDriver driver)
    {
        driver.Navigate().Refresh();
    }
    
    public static ICookieJar GetCookieJar(this IWebDriver driver)
    {
        return driver.Manage().Cookies;
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

    public static void SetCookie(this ICookieJar cookieJar, string cookieName, string newCookieValue)
    {
        var newCookie = new Cookie(cookieName, newCookieValue);
        var cookie = cookieJar.GetCookieNamed(cookieName);
        if (cookie is not null)
        {
            cookieJar.DeleteCookie(cookie);
            newCookie = new Cookie(cookieName, newCookieValue, cookie.Domain, cookie.Path, cookie.Expiry, cookie.Secure,
                cookie.IsHttpOnly, cookie.SameSite);
        }
        
        cookieJar.AddCookie(newCookie);
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
        ((IJavaScriptExecutor) driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
    }

    public static void Click(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor) driver).ExecuteScript("arguments[0].click();", element);
    }
    
    public static void RemoveElement(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor) driver).ExecuteScript("arguments[0].remove();", element);
    }
    
    public static long GetScrollHeight(this IWebDriver driver)
    {
        var height = ((IJavaScriptExecutor) driver).ExecuteScript("return window.pageYOffset;");
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
        var seleniumCookie = new Cookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, expiration, cookie.Secure, 
            cookie.HttpOnly, cookie.SameSite);
        return seleniumCookie;
    }
}