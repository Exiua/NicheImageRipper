using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DataStructures;

namespace Gui.Models.Full;

public class FullRipperClient : IRipperClient
{
    public int UrlQueueCount { get; }
    
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;
    
    public Task Rip()
    {
        throw new NotImplementedException();
    }

    public bool Resume()
    {
        throw new NotImplementedException();
    }

    public bool Pause()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetUrlQueue()
    {
        throw new NotImplementedException();
    }

    public RejectedUrlsInfo QueueUrls(string url)
    {
        throw new NotImplementedException();
    }

    public void DequeueUrls(IEnumerable<string> url)
    {
        throw new NotImplementedException();
    }

    public void ForceQueueUrl(string url)
    {
        throw new NotImplementedException();
    }

    public void RequeueUrls(RejectedUrlsInfo rejectedUrls)
    {
        throw new NotImplementedException();
    }

    public void LoadUrlFile(string path)
    {
        throw new NotImplementedException();
    }

    public void SaveData()
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}