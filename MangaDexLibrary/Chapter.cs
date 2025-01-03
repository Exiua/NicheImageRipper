using System.Text.Json;
using MangaDexLibrary.Responses;

namespace MangaDexLibrary;

public class Chapter
{
    private HttpClient _client;
    
    public Chapter(HttpClient client)
    {
        _client = client;
    }

    public async Task<MangaDexResponse> GetChapter(Guid chapterId)
    {
        var url = $"https://api.mangadex.org/chapter/{chapterId}";
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(content) ?? new ErrorResponse();
            return error;
        }
        
        var chapter = JsonSerializer.Deserialize<AtHomeResponse>(content);
    }
}