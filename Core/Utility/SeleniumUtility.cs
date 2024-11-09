using System.Diagnostics;
using OpenQA.Selenium;

namespace Core.Utility;

public static class SeleniumUtility
{
    [Conditional("DEBUG")]
    public static void TakeDebugScreenshot(this IWebDriver driver)
    {
        ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile("test.png");
    }
}