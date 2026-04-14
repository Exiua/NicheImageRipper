using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DataStructures;

namespace Gui.Services;

public interface IRipperClient : IDisposable
{
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public int UrlQueueCount { get; }

    public Task Rip();
    public Task<bool> Resume();
    public Task<bool> Pause();
    public Task<IEnumerable<string>> GetUrlQueue();
    public Task<RejectedUrlsInfo> QueueUrls(string url);
    public Task DequeueUrls(IEnumerable<string> url);
    public Task ForceQueueUrl(string url);
    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls);
    public Task LoadUrlFile(string path);
    public Task SaveData();
}