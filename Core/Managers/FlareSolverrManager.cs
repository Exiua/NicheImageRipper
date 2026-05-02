using Core.Exceptions;
using FlareSolverrIntegration;
using FlareSolverrIntegration.Payloads;
using FlareSolverrIntegration.Responses;
using Serilog;

namespace Core.Managers;

public class FlareSolverrManager(string flareSolverrUri)
{
    private readonly FlareSolverrClient _flareSolverrClient = new(flareSolverrUri);
    private readonly ILogger _logger = Log.ForContext<FlareSolverrManager>();
    
    private string? _sessionId;

    private async Task CreateSession()
    {
        var response = await _flareSolverrClient.CreateSession();
        if (response is not SessionCreationResponse sessionCreationResponse)
        {
            throw new FailedToCreateSession();
        }
        
        _sessionId = sessionCreationResponse.Session;
    }

    private async Task GetSession()
    {
        var response = await _flareSolverrClient.ListSessions();
        if (response is not SessionListResponse sessionListResponse)
        {
            throw new FailedToListSessionsException();
        }
        
        if (sessionListResponse.Sessions.Count == 0)
        {
            await CreateSession();
        }
        else
        {
            _sessionId = sessionListResponse.Sessions[0];
        }
    }
    
    public async Task DeleteSession(bool suppressException = false)
    {
        if (_sessionId is null)
        {
            return;
        }
        
        var response = await _flareSolverrClient.DeleteSession(_sessionId);
        if (response.Status != "ok" && !suppressException)
        {
            throw new FailedToDeleteSessionException();
        }
        
        _sessionId = null;
    }
    
    public async Task<Solution> GetSiteSolution(string url, List<Dictionary<string, string>>? cookies = null)
    {
        _logger.Debug("Getting site solution for {Url}", url);
        if (_sessionId is null)
        {
            await GetSession();
        }
        
        var payload = GetRequestPayload.SetUrl(url).SetSession(_sessionId!).SetCookies(cookies);
        var response = await _flareSolverrClient.GetRequest(payload);
        if (response is not RequestResponse requestResponse)
        {
            throw new FailedToGetSolutionException();
        }

        _logger.Debug("Got site solution");
        return requestResponse.Solution;
    }
}