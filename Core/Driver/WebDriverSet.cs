using NicheImageRipper.Sdk.Driver;
using Serilog;

namespace NicheImageRipper.Core.Driver;

/// <summary>
///     A capped set of WebDrivers of one kind (headless or not). The semaphore counts free capacity: idle drivers
///     plus drivers not yet created. Every driver handed out is tracked as leased until it's released.
/// </summary>
public class WebDriverSet : IDisposable
{
    private readonly List<WebDriver> _idleDrivers = [];
    private readonly HashSet<WebDriver> _leasedDrivers = new(ReferenceEqualityComparer.Instance);
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxCapacity;
    private readonly bool _headless;
    private readonly ILogger _logger;

    private bool _disposed;

    /// <summary>Drivers that currently exist (idle or leased).</summary>
    public int CurrentCount
    {
        get
        {
            lock (_lock)
            {
                return _idleDrivers.Count + _leasedDrivers.Count;
            }
        }
    }

    public WebDriverSet(int maxCapacity, bool headless)
    {
        _headless = headless;
        _maxCapacity = maxCapacity;
        _semaphore = new SemaphoreSlim(maxCapacity, maxCapacity);
        _logger = Log.ForContext<WebDriverSet>();
    }

    /// <summary>Leases an idle driver (health-checked first) or creates one, blocking while at capacity.</summary>
    public WebDriver AcquireDriver()
    {
        // Block the thread if no resources are available
        _logger.Debug("Waiting for an available WebDriver {{ headless = {Headless} }}", _headless);
        _semaphore.Wait();
        _logger.Debug("Acquired a WebDriver {{ headless = {Headless} }}", _headless);

        WebDriver? idleDriver = null;
        lock (_lock)
        {
            if (_idleDrivers.Count > 0)
            {
                idleDriver = _idleDrivers[^1];
                _idleDrivers.RemoveAt(_idleDrivers.Count - 1);
                _leasedDrivers.Add(idleDriver);
            }
            else if (_leasedDrivers.Count >= _maxCapacity)
            {
                _semaphore.Release();
                throw new InvalidOperationException("This should not happen due to the semaphore.");
            }
        }

        if (idleDriver is not null)
        {
            _logger.Debug("Reusing an existing WebDriver {{ headless = {Headless} }}", _headless);
            // Outside the lock: the health check loads a page
            return CheckWebDriverHealth(idleDriver);
        }

        _logger.Debug("Creating a new WebDriver {{ headless = {Headless} }}", _headless);
        WebDriver driver;
        try
        {
            driver = CreateFirefoxDriver();
        }
        catch
        {
            _semaphore.Release();
            throw;
        }

        lock (_lock)
        {
            _leasedDrivers.Add(driver);
        }

        return driver;
    }

    /// <summary>
    ///     Returns a leased driver to the idle set. Releasing a driver that isn't currently leased (e.g. a second
    ///     release of the same driver) is logged and ignored, since releasing it again would overflow the semaphore.
    /// </summary>
    public void ReleaseDriver(WebDriver driver)
    {
        _logger.Debug("Releasing a WebDriver {{ headless = {Headless} }}", _headless);
        ArgumentNullException.ThrowIfNull(driver);

        lock (_lock)
        {
            if (!_leasedDrivers.Remove(driver))
            {
                _logger.Warning("Ignoring release of a WebDriver that isn't leased (already released?) " +
                                "{{ headless = {Headless} }}", _headless);
                return;
            }

            _idleDrivers.Add(driver);
        }

        // Release the semaphore to unblock waiting threads
        _semaphore.Release();
        _logger.Debug("Released a WebDriver {{ headless = {Headless} }}", _headless);
    }

    /// <summary>Destroys one idle driver to free room for the other kind. Does nothing if none are idle.</summary>
    public void DestroyDriver()
    {
        WebDriver driver;
        lock (_lock)
        {
            if (_idleDrivers.Count == 0)
            {
                return;
            }

            driver = _idleDrivers[^1];
            _idleDrivers.RemoveAt(_idleDrivers.Count - 1);
        }

        // No semaphore change: an idle driver and a not-yet-created one both count as free capacity
        _logger.Debug("Destroying a WebDriver {{ headless = {Headless} }}", _headless);
        try
        {
            driver.Dispose();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to dispose of a WebDriver {{ headless = {Headless} }}", _headless);
        }

        _logger.Debug("Destroyed a WebDriver {{ headless = {Headless} }}", _headless);
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
            _logger.Error(e, "WebDriver is unreachable. Regenerating the driver.");
            driver.RegenerateDriver();
            return driver;
        }
    }

    private WebDriver CreateFirefoxDriver()
    {
        return new WebDriver(_headless);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _semaphore.Dispose();
        List<WebDriver> idleDrivers;
        lock (_lock)
        {
            idleDrivers = [.._idleDrivers];
            _idleDrivers.Clear();
        }

        foreach (var driver in idleDrivers)
        {
            try
            {
                driver.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to dispose of a WebDriver {{ headless = {Headless} }}", _headless);
            }
        }

        GC.SuppressFinalize(this);
    }

    ~WebDriverSet()
    {
        Dispose();
    }
}
