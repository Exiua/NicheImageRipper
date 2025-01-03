namespace MangaDexLibrary;

public class MangaDexClient : IDisposable
{
    private HttpClient _client;
    
    public AtHome AtHome { get; set; }
    public Chapter Chapter { get; set; }
    
    public MangaDexClient() : this(new HttpClient())
    {
        
    }

    public MangaDexClient(HttpClient client)
    {
        _client = client;
        AtHome = new AtHome(_client);
        Chapter = new Chapter(_client);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}