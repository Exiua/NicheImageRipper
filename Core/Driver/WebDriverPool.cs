namespace Core.Driver;

public class WebDriverPool : IDisposable
{
    private readonly WebDriverSet _availableHeadlessDrivers;
    private readonly WebDriverSet _availableNonHeadlessDrivers;
    
    public WebDriverPool(int maxCapacity)
    {
        _availableHeadlessDrivers = new WebDriverSet(maxCapacity);
        _availableNonHeadlessDrivers = new WebDriverSet(maxCapacity);
    }
    
    public WebDriver AcquireDriver(bool headless)
    {
        return headless 
            ? _availableHeadlessDrivers.AcquireDriver(true) 
            : _availableNonHeadlessDrivers.AcquireDriver(false);
    }
    
    public void ReleaseDriver(WebDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        
        if (driver.IsHeadless)
        {
            _availableHeadlessDrivers.ReleaseDriver(driver);
        }
        else
        {
            _availableNonHeadlessDrivers.ReleaseDriver(driver);
        }
    }
    
    public void DestroyDriver(WebDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        
        if (driver.IsHeadless)
        {
            _availableHeadlessDrivers.DestroyDriver(driver);
        }
        else
        {
            _availableNonHeadlessDrivers.DestroyDriver(driver);
        }
    }

    public void Dispose()
    {
        _availableHeadlessDrivers.Dispose();
        _availableNonHeadlessDrivers.Dispose();
        GC.SuppressFinalize(this);
    }
}