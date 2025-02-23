using System.Collections.Concurrent;
using Core.SiteParsing;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using Serilog;

namespace Core.History;

public class WebDriverPool : IDisposable
{
    private readonly ConcurrentBag<WebDriver> _availableDrivers = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore;
    private int _currentCount;
    private readonly int _maxCapacity;
    
    public WebDriverPool(int maxCapacity)
    {
        _maxCapacity = maxCapacity;
        _semaphore = new SemaphoreSlim(maxCapacity, maxCapacity);
    }
    
    public WebDriver AcquireDriver(bool debug)
    {
        // Block the thread if no resources are available
        Log.Debug("Waiting for an available WebDriver.");
        _semaphore.Wait();
        Log.Debug("Acquired a WebDriver.");

        lock (_lock)
        {
            if (_availableDrivers.TryTake(out var driver))
            {
                Log.Debug("Reusing an existing WebDriver.");
                driver = CheckWebDriverHealth(driver, debug);
                return driver; // Reuse an available driver
            }

            if (_currentCount < _maxCapacity)
            {
                _currentCount++;
                Log.Debug("Creating a new WebDriver.");
                try
                {
                    return CreateFirefoxDriver(debug); // Create a new driver if under capacity
                }
                catch
                {
                    _currentCount--;
                    _semaphore.Release();
                    throw;
                }
            }

            throw new InvalidOperationException("This should not happen due to the semaphore.");
        }
    }
    
    public void ReleaseDriver(WebDriver driver)
    {
        Log.Debug("Releasing a WebDriver.");
        ArgumentNullException.ThrowIfNull(driver);

        lock (_lock)
        {
            _availableDrivers.Add(driver);
        }

        // Release the semaphore to unblock waiting threads
        _semaphore.Release();
        Log.Debug("Released a WebDriver.");
    }
    
    private static WebDriver CheckWebDriverHealth(WebDriver driver, bool debug)
    {
        try
        {
            driver.Driver.Url = "https://www.google.com";
            return driver;
        }
        catch (Exception e)
        {
            Log.Error(e, "WebDriver is unreachable. Regenerating the driver.");
            driver.RegenerateDriver(debug);
            return driver;
        }
    }

    private static WebDriver CreateFirefoxDriver(bool debug)
    {
        return new WebDriver(debug);
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
                Log.Error(e, "Failed to dispose of a WebDriver.");
            }
        }
        
        _availableDrivers.Clear();
        GC.SuppressFinalize(this);
    }
}