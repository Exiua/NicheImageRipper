namespace MangaDexLibrary;

public class MangaDexClient : IDisposable
{
    private HttpClient _client;
    
    public AtHome AtHome { get; set; }
    public Chapter Chapter { get; set; }
    public Manga Manga { get; set; }
    
    public MangaDexClient() : this(new HttpClient())
    {
        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("MangaDexLibrary");
        }
    }

    public MangaDexClient(HttpClient client)
    {
        _client = client;
        AtHome = new AtHome(_client);
        Chapter = new Chapter(_client);
        Manga = new Manga(_client);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}