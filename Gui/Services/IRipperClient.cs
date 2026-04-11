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
    public bool Resume();
    public bool Pause();
    public IEnumerable<string> GetUrlQueue();
    public RejectedUrlsInfo QueueUrls(string url);
    public void DequeueUrls(IEnumerable<string> url);
    public void ForceQueueUrl(string url);
    public void RequeueUrls(RejectedUrlsInfo rejectedUrls);
    public void LoadUrlFile(string path);
    public void SaveData();
}