using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DataStructures;

namespace Gui.Services.Thin;

public class RemoteRipperClient(IBackendConnector backendConnector) : IRipperClient
{
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public int UrlQueueCount => backendConnector.GetQueueCountAsync().Result;
    
    public Task Rip()
    {
        return backendConnector.RipAsync();
    }

    public bool Resume()
    {
        return backendConnector.ResumeAsync().Result;
    }

    public bool Pause()
    {
        return backendConnector.PauseAsync().Result;
    }

    public IEnumerable<string> GetUrlQueue()
    {
        return backendConnector.GetQueueSnapshotAsync().Result;
    }

    public RejectedUrlsInfo QueueUrls(string url)
    {
        //return backendConnector.QueueUrlsAsync([url]).Result;
        throw new NotImplementedException();
    }

    public void DequeueUrls(IEnumerable<string> url)
    {
        backendConnector.DequeueUrlsAsync(url as string[] ?? url.ToArray()).Wait();
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
        throw new NotImplementedException();
    }
}