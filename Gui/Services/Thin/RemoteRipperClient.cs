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

    public Task<bool> Resume()
    {
        return backendConnector.ResumeAsync();
    }

    public Task<bool> Pause()
    {
        return backendConnector.PauseAsync();
    }

    public async Task<IEnumerable<string>> GetUrlQueue()
    {
        var queue = await backendConnector.GetQueueSnapshotAsync();
        return queue;
    }

    public Task<RejectedUrlsInfo> QueueUrls(string url)
    {
        //return backendConnector.QueueUrlsAsync([url]);
        throw new NotImplementedException();
    }

    public Task DequeueUrls(IEnumerable<string> url)
    {
        return backendConnector.DequeueUrlsAsync(url as string[] ?? url.ToArray());
    }

    public Task ForceQueueUrl(string url)
    {
        throw new NotImplementedException();
    }

    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls)
    {
        throw new NotImplementedException();
    }

    public Task LoadUrlFile(string path)
    {
        throw new NotImplementedException();
    }

    public Task SaveData()
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}