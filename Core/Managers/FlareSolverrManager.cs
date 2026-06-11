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

    private async Task CreateSession(CancellationToken cancellationToken = default)
    {
        var response = await _flareSolverrClient.CreateSession(cancellationToken: cancellationToken);
        if (response is not SessionCreationResponse sessionCreationResponse)
        {
            throw new FailedToCreateSession();
        }
        
        _sessionId = sessionCreationResponse.Session;
    }

    private async Task GetSession(CancellationToken cancellationToken = default)
    {
        var response = await _flareSolverrClient.ListSessions(cancellationToken: cancellationToken);
        if (response is not SessionListResponse sessionListResponse)
        {
            throw new FailedToListSessionsException();
        }
        
        if (sessionListResponse.Sessions.Count == 0)
        {
            await CreateSession(cancellationToken);
        }
        else
        {
            _sessionId = sessionListResponse.Sessions[0];
        }
    }
    
    public async Task DeleteSession(bool suppressException = false, CancellationToken cancellationToken = default)
    {
        if (_sessionId is null)
        {
            return;
        }
        
        var response = await _flareSolverrClient.DeleteSession(_sessionId, cancellationToken: cancellationToken);
        if (response.Status != "ok" && !suppressException)
        {
            throw new FailedToDeleteSessionException();
        }
        
        _sessionId = null;
    }
    
    public async Task<Solution> GetSiteSolution(string url, List<Dictionary<string, string>>? cookies = null, CancellationToken cancellationToken = default)
    {
        _logger.Debug("Getting site solution for {Url}", url);
        if (_sessionId is null)
        {
            await GetSession(cancellationToken);
        }
        
        var payload = GetRequestPayload.SetUrl(url).SetSession(_sessionId!).SetCookies(cookies);
        var response = await _flareSolverrClient.GetRequest(payload, cancellationToken: cancellationToken);
        if (response is not RequestResponse requestResponse)
        {
            throw new FailedToGetSolutionException();
        }

        _logger.Debug("Got site solution");
        return requestResponse.Solution;
    }
}