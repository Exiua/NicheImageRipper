namespace Core.Driver;

public class WebDriverPool : IDisposable
{
    private readonly WebDriverSet _availableHeadlessDrivers;
    private readonly WebDriverSet _availableNonHeadlessDrivers;
    private readonly int _maxCapacity;
    
    private int CurrentCount => _availableHeadlessDrivers.CurrentCount + _availableNonHeadlessDrivers.CurrentCount;
    
    public WebDriverPool(int maxCapacity)
    {
        _maxCapacity = maxCapacity;
        _availableHeadlessDrivers = new WebDriverSet(maxCapacity, true);
        _availableNonHeadlessDrivers = new WebDriverSet(maxCapacity, false);
    }
    
    public WebDriver AcquireDriver(bool headless)
    {
        if (headless)
        {
            if (CurrentCount >= _maxCapacity)
            {
                _availableNonHeadlessDrivers.DestroyDriver();
            }
            
            return _availableHeadlessDrivers.AcquireDriver();
        }

        if (CurrentCount >= _maxCapacity)
        {
            _availableHeadlessDrivers.DestroyDriver();
        }
        
        return _availableNonHeadlessDrivers.AcquireDriver();
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

    public void Dispose()
    {
        _availableHeadlessDrivers.Dispose();
        _availableNonHeadlessDrivers.Dispose();
        GC.SuppressFinalize(this);
    }
}