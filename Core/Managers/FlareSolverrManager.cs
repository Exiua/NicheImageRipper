using Core.Exceptions;
using FlareSolverrIntegration;
using FlareSolverrIntegration.Payloads;
using FlareSolverrIntegration.Responses;

namespace Core.Managers;

public class FlareSolverrManager
{
    private readonly FlareSolverrClient _flareSolverrClient;
    private string? _sessionId;
    
    public FlareSolverrManager(string flareSolverrUri)
    {
        _flareSolverrClient = new FlareSolverrClient(flareSolverrUri);
    }
    
    private async Task CreateSession()
    {
        var response = await _flareSolverrClient.CreateSession();
        if (response is not SessionCreationResponse sessionCreationResponse)
        {
            throw new RipperException("Failed to create session");
        }
        
        _sessionId = sessionCreationResponse.Session;
    }
    
    public async Task DeleteSession()
    {
        if (_sessionId is null)
        {
            return;
        }
        
        var response = await _flareSolverrClient.DeleteSession(_sessionId);
        if (response.Status != "ok")
        {
            throw new RipperException("Failed to delete session");
        }
        
        _sessionId = null;
    }
    
    public async Task<Solution> GetSiteSolution(string url)
    {
        if (_sessionId is null)
        {
            await CreateSession();
        }
        
        var payload = GetRequestPayload.SetUrl(url).SetSession(_sessionId!);
        var response = await _flareSolverrClient.GetRequest(payload);
        if (response is not RequestResponse requestResponse)
        {
            throw new RipperException("Failed to get site solution");
        }

        return requestResponse.Solution;
    }
}