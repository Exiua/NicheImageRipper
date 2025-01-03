using System.Text.Json;
using MangaDexLibrary.Responses;

namespace MangaDexLibrary;

public class Manga
{
    private HttpClient _client;
    
    public Manga(HttpClient client)
    {
        _client = client;
    }

    public  Task<MangaDexResponse> GetVolumeAndChapter(Guid mangaId)
    {
        return GetVolumeAndChapter(mangaId.ToString());
    }

    public async Task<MangaDexResponse> GetVolumeAndChapter(string mangaId)
    {
        var url = $"https://api.mangadex.org/manga/{mangaId}/aggregate";
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(content) ?? new ErrorResponse();
            return error;
        }
        
        var manga = JsonSerializer.Deserialize<AggregateMangaResponse>(content);
        if (manga is null)
        {
            return new ErrorResponse();
        }
        
        return manga;
    }
}