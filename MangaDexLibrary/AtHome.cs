using System.Text.Json;
using MangaDexLibrary.Responses;

namespace MangaDexLibrary;

public class AtHome
{
    private HttpClient _client;
    
    public AtHome(HttpClient client)
    {
        _client = client;
    }
    
    public async Task<MangaDexResponse> GetServerUrls(Guid chapterId)
    {
        var url = $"https://api.mangadex.org/at-home/server/{chapterId}";
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(content) ?? new ErrorResponse();
            return error;
        }
        
        var serverUrls = JsonSerializer.Deserialize<AtHomeResponse>(content);
        if (serverUrls is null)
        {
            return new ErrorResponse();
        }
        
        return serverUrls;
    }
}