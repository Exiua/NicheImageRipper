using System.Collections.Concurrent;
using OpenQA.Selenium;
using Serilog;

namespace Core.History;

public class WebDriverPool
{
    private readonly ConcurrentBag<IWebDriver> _availableDrivers = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore;
    private int _currentCount;
    private readonly int _maxCapacity;
    
    public WebDriverPool(int maxCapacity)
    {
        _maxCapacity = maxCapacity;
        _semaphore = new SemaphoreSlim(maxCapacity, maxCapacity);
    }
    
    public IWebDriver AcquireDriver()
    {
        // Block the thread if no resources are available
        _semaphore.Wait();

        lock (_lock)
        {
            if (_availableDrivers.TryTake(out var driver))
            {
                Log.Debug("Reusing an existing WebDriver.");
                return driver; // Reuse an available driver
            }

            if (_currentCount < _maxCapacity)
            {
                _currentCount++;
                Log.Debug("Creating a new WebDriver.");
                return CreateWebDriver(); // Create a new driver if under capacity
            }

            throw new InvalidOperationException("This should not happen due to the semaphore.");
        }
    }
    
    public void ReleaseDriver(IWebDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        lock (_lock)
        {
            _availableDrivers.Add(driver);
        }

        // Release the semaphore to unblock waiting threads
        _semaphore.Release();
    }

    private IWebDriver CreateWebDriver()
    {
        throw new NotImplementedException();
    }
}