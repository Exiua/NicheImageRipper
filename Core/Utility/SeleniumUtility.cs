using System.Diagnostics;
using OpenQA.Selenium;

namespace Core.Utility;

public static class SeleniumUtility
{
    [Conditional("DEBUG")]
    public static void TakeDebugScreenshot(this IWebDriver driver, string filename = "test.png")
    {
        ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(filename);
    }
    
    [Conditional("DEBUG")]
    public static void DumpHtml(this IWebDriver driver)
    {
        var html = driver.PageSource;
        if (string.IsNullOrEmpty(html))
        {
            Debug.WriteLine("HTML is empty or null.");
            return;
        }
        
        File.WriteAllText("test.html", html);
    }

    [Conditional("DEBUG")]
    public static void DumpCookies(this IWebDriver driver)
    {
        List<Dictionary<string, object?>> cookies = [];
        cookies.AddRange(driver.Manage()
                               .Cookies.AllCookies.Select(cookie => new Dictionary<string, object?>
                                {
                                    ["name"] = cookie.Name,
                                    ["value"] = cookie.Value,
                                    ["domain"] = cookie.Domain,
                                    ["path"] = cookie.Path,
                                    ["expiry"] = cookie.Expiry,
                                    ["secure"] = cookie.Secure,
                                    ["httpOnly"] = cookie.IsHttpOnly,
                                    ["session"] = cookie.Expiry == null
                                }));
        
        JsonUtility.Serialize("debug_cookies.json", cookies);
    }
}