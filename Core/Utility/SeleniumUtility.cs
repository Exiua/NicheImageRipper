using OpenQA.Selenium;

namespace Core.Utility;

public static class SeleniumUtility
{
    public static bool ElementIsVisible(IWebDriver driver, By by)
    {
        try
        {
            var elementToBeDisplayed = driver.FindElement(by);
            return elementToBeDisplayed.Displayed;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }
}