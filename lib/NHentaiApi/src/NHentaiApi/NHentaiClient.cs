using System.Net;
using System.Net.Http.Json;
using NHentaiApi.Models;
using NHentaiApi.Models.Responses;

namespace NHentaiApi;

public class NHentaiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    private bool _disposed;

    public NHentaiClient(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");
    }

    public async Task<GetResponse<Gallery>> GetGalleryAsync(int galleryId, CancellationToken cancellationToken = default)
    {
        var response =
            await _httpClient.GetAsync($"https://nhentai.net/api/v2/galleries/{galleryId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return GetResponse<Gallery>.RateLimited("Rate limited by nhentai.net API.");
            }

            var errorMessage = $"Failed to get gallery. Status code: {response.StatusCode}";
            return GetResponse<Gallery>.Error(errorMessage);
        }

        var gallery = await response.Content.ReadFromJsonAsync<Gallery>(cancellationToken: cancellationToken);
        return gallery is null
            ? GetResponse<Gallery>.Error("Failed to deserialize gallery response.")
            : GetResponse<Gallery>.Success(gallery);
    }

    public async Task<GetResponse<GalleryDownloadUrl>> GetGalleryDownloadAsync(
        int galleryId, CancellationToken cancellationToken = default)
    {
        var response =
            await _httpClient.GetAsync($"https://nhentai.net/api/v2/galleries/{galleryId}/download?format=cbz", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return GetResponse<GalleryDownloadUrl>.RateLimited("Rate limited by nhentai.net API.");
            }

            var errorMessage = $"Failed to get gallery download. Status code: {response.StatusCode}";
            return GetResponse<GalleryDownloadUrl>.Error(errorMessage);
        }

        var gallery = await response.Content.ReadFromJsonAsync<GalleryDownloadUrl>(cancellationToken: cancellationToken);
        return gallery is null
            ? GetResponse<GalleryDownloadUrl>.Error("Failed to deserialize gallery download response.")
            : GetResponse<GalleryDownloadUrl>.Success(gallery);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    ~NHentaiClient()
    {
        Dispose();
    }
}