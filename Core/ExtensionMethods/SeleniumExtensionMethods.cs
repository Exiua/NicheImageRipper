﻿using OpenQA.Selenium;

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

    /// <summary>
    ///     Adds a cookie to the current session. Intended for adding a single cookie. If you need to add multiple
    /// cookies, use the cookie jar directly.
    /// </summary>
    /// <param name="driver"></param>
    /// <param name="cookieName"></param>
    /// <param name="cookieValue"></param>
    public static void AddCookie(this IWebDriver driver, string cookieName, string cookieValue)
    {
        driver.GetCookieJar().AddCookie(new Cookie(cookieName, cookieValue));   
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
    
    public static string? GetSrc(this IWebElement element)
    {
        return element.GetAttribute("src");
    }
    
    public static string? GetHref(this IWebElement element)
    {
        return element.GetAttribute("href");
    }
    
    public static void ScrollElementIntoView(this IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor) driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
    }
}