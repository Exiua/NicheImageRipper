using Core;
using Core.DataStructures;
using Core.History;
using CoreService.Models.Dtos;

namespace CoreService.Singletons;

public class NicheImageRipperSingleton(ILogger<NicheImageRipperSingleton> logger) : INicheImageRipperSingleton
{
    private readonly NicheImageRipper _ripper = new();
    private readonly Lock _ripperLock = new();
    private bool _isRipping;
    
    public string[] GetQueueSnapshot()
    {
        using (_ripperLock.EnterScope())
        {
            return _ripper.UrlQueue.Select(u => u).ToArray();
        }
    }

    public IEnumerable<RejectedUrlInfoDto> Queue(string[] urls)
    {
        using (_ripperLock.EnterScope())
        {
            var rejected = urls.Select(url => _ripper.QueueUrls(url))
                               .Where(rejectedUrlsInfo => rejectedUrlsInfo.HasRejectedUrls)
                               .SelectMany(rejectedUrlsInfo => rejectedUrlsInfo.Urls)
                               .Select(rejectedUrl => new RejectedUrlInfoDto(rejectedUrl.Url, rejectedUrl.Reason))
                               .ToList();
            return rejected;
        }
    }

    public void Dequeue(string[] urls)
    {
        using (_ripperLock.EnterScope())
        {
            _ripper.DequeueUrls(urls);
        }
    }

    public bool Rip()
    {
        if (Interlocked.Exchange(ref _isRipping, true))
        {
            return false;
        }
        
        Task.Run(async () =>
        {
            try
            {
                await _ripper.Rip();
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while ripping.");
            }
            finally
            {
                Interlocked.Exchange(ref _isRipping, false);
            }
        });
        
        return true;
    }

    public bool IsRipping => _isRipping;
    public bool Paused => _ripper.Paused;
    
    public bool Pause()
    {
        if (!_isRipping || Paused)
        {
            return false;
        }
        
        _ripper.Pause();
        return true;
    }

    public bool Resume()
    {
        if (!_isRipping || !Paused)
        {
            return false;
        }
        
        _ripper.Resume();
        return true;
    }

    public IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null)
    {
        return NicheImageRipper.GetHistoryPage(start, offset, filter);
    }

    public int GetHistoryCount()
    {
        return NicheImageRipper.GetHistoryCount();
    }
}