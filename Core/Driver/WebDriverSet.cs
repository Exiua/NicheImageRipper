using System.Collections.Concurrent;
using Serilog;

namespace Core.Driver;

public class WebDriverSet : IDisposable
{
    private readonly ConcurrentBag<WebDriver> _availableDrivers = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxCapacity;
    private readonly bool _headless;

    public int CurrentCount { get; set; }

    public WebDriverSet(int maxCapacity, bool headless)
    {
        _headless = headless;
        _maxCapacity = maxCapacity;
        _semaphore = new SemaphoreSlim(maxCapacity, maxCapacity);
    }
    
    public WebDriver AcquireDriver(bool checkHealth = true)
    {
        // Block the thread if no resources are available
        Log.Debug("Waiting for an available WebDriver {{ headless = {Headless} }}", _headless);
        _semaphore.Wait();
        Log.Debug("Acquired a WebDriver {{ headless = {Headless} }}", _headless);

        lock (_lock)
        {
            if (_availableDrivers.TryTake(out var driver))
            {
                Log.Debug("Reusing an existing WebDriver {{ headless = {Headless} }}", _headless);
                if (true)
                {
                    driver = CheckWebDriverHealth(driver);
                }
                return driver; // Reuse an available driver
            }

            if (CurrentCount < _maxCapacity)
            {
                if (!checkHealth)
                {
                    // TODO: This is a warning because it is not expected to create a new driver when not checking health.
                    // TODO:    When check health is false, it is expected to reuse an existing driver to destroy in DestroyDriver().
                    Log.Warning("CheckHealth is false when retrieving an existing driver to destroy. Probably do not want to create a new driver.");
                }
                
                CurrentCount++;
                Log.Debug("Creating a new WebDriver {{ headless = {Headless} }}", _headless);
                try
                {
                    return CreateFirefoxDriver(); // Create a new driver if under capacity
                }
                catch
                {
                    CurrentCount--;
                    _semaphore.Release();
                    throw;
                }
            }

            throw new InvalidOperationException("This should not happen due to the semaphore.");
        }
    }
    
    public void ReleaseDriver(WebDriver driver)
    {
        Log.Debug("Releasing a WebDriver {{ headless = {Headless} }}", _headless);
        ArgumentNullException.ThrowIfNull(driver);

        lock (_lock)
        {
            _availableDrivers.Add(driver);
        }

        // Release the semaphore to unblock waiting threads
        _semaphore.Release();
        Log.Debug("Released a WebDriver {{ headless = {Headless} }}", _headless);
    }

    public void DestroyDriver()
    {
        if (_availableDrivers.IsEmpty)
        {
            return;
        }
        
        var driver = AcquireDriver(false); 
        DestroyDriver(driver);
    }

    private void DestroyDriver(WebDriver driver)
    {
        Log.Debug("Destroying a WebDriver {{ headless = {Headless} }}", _headless);
        ArgumentNullException.ThrowIfNull(driver);

        lock (_lock)
        {
            driver.Dispose();
            CurrentCount--;
        }
        
        // Release the semaphore to unblock waiting threads
        _semaphore.Release();
        Log.Debug("Destroyed a WebDriver {{ headless = {Headless} }}", _headless);
    }
    
    private WebDriver CheckWebDriverHealth(WebDriver driver)
    {
        try
        {
            driver.Driver.Url = "https://www.google.com";
            return driver;
        }
        catch (Exception e)
        {
            Log.Error(e, "WebDriver is unreachable. Regenerating the driver.");
            driver.RegenerateDriver(_headless);
            return driver;
        }
    }

    private WebDriver CreateFirefoxDriver()
    {
        return new WebDriver(_headless);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        foreach (var driver in _availableDrivers)
        {
            try
            {
                driver.Dispose();
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to dispose of a WebDriver {{ headless = {Headless} }}", _headless);
            }
        }
        
        _availableDrivers.Clear();
        GC.SuppressFinalize(this);
    }
}