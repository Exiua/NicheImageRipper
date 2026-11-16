using Serilog;
using SteamKit2;
using SteamKit2.Authentication;

namespace SteamApiClient;

/// <summary>
/// Owns the Steam connection: callback pump, authentication, and automatic reconnection. A logon is represented by a
/// single <see cref="TaskCompletionSource"/>; callers await it via <see cref="LoginAsync"/> or
/// <see cref="EnsureLoggedOnAsync"/>, so concurrent callers share one in-flight logon.
/// </summary>
internal sealed class SteamSession : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SteamSession>();

    private readonly SteamApiClientOptions _options;
    private readonly CallbackManager _manager;
    private readonly SteamUser _steamUser;
    private readonly CancellationTokenSource _pumpCts = new();
    private readonly Task _pumpTask;
    private readonly Lock _gate = new();

    // All fields below are guarded by _gate
    private TaskCompletionSource? _logonTcs;
    private string _username = "";
    private string _password = "";
    private int _generation;
    private int _reconnectAttempts;
    private bool _usedStoredToken;
    private bool _disposed;

    public SteamSession(SteamApiClientOptions options)
    {
        _options = options;
        Client = new SteamClient();
        _manager = new CallbackManager(Client);
        _steamUser = Client.GetHandler<SteamUser>()!;
        Apps = Client.GetHandler<SteamApps>()!;
        UnifiedMessages = Client.GetHandler<SteamUnifiedMessages>()!;
        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _pumpTask = Task.Run(() => PumpCallbacks(_pumpCts.Token));
    }

    public SteamClient Client { get; }
    public SteamApps Apps { get; }
    public SteamUnifiedMessages UnifiedMessages { get; }

    private void PumpCallbacks(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error while pumping Steam callbacks");
            }
        }
    }

    public Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        Task logonTask;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_logonTcs is { Task.IsFaulted: false, Task.IsCanceled: false } &&
                string.Equals(_username, username, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Debug("Logon for {Username} already completed or in progress", username);
                return _logonTcs.Task.WaitAsync(cancellationToken);
            }

            Logger.Debug("Logging in as {Username}", username);
            _username = username;
            _password = password;
            logonTask = StartLogon();
        }

        // Connect() tears down any existing connection first; that disconnect is user-initiated and ignored.
        Client.Connect();
        return logonTask.WaitAsync(cancellationToken);
    }

    /// <summary>Waits for the current logon (including an in-progress reconnect) to complete.</summary>
    public Task EnsureLoggedOnAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _logonTcs?.Task.WaitAsync(cancellationToken) ??
                   throw new SteamApiException("Must call LoginAsync before using the Steam session.");
        }
    }

    /// <summary>Drops the current connection and logs on again with the same credentials.</summary>
    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        Task logonTask;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrEmpty(_username))
            {
                throw new SteamApiException("Must call LoginAsync before reconnecting.");
            }

            // A reconnect is already underway; share it rather than restarting it
            if (_logonTcs is { Task.IsCompleted: false })
            {
                return _logonTcs.Task.WaitAsync(cancellationToken);
            }

            Logger.Information("Reconnecting to Steam");
            logonTask = StartLogon();
        }

        Client.Connect();
        return logonTask.WaitAsync(cancellationToken);
    }

    public Task LogoutAsync()
    {
        lock (_gate)
        {
            Logger.Information("Logging out");
            _generation++;
            _logonTcs?.TrySetException(new SteamApiException("Logged out before logon completed."));
            _logonTcs = null;
            _username = "";
            _password = "";
        }

        if (Client.SessionID is not null)
        {
            _steamUser.LogOff();
        }

        Client.Disconnect();
        return Task.CompletedTask;
    }

    /// <summary>Replaces the current logon with a new pending one. Must be called while holding <see cref="_gate"/>.</summary>
    private Task StartLogon()
    {
        _generation++;
        _reconnectAttempts = 0;
        _logonTcs?.TrySetException(new SteamApiException("Logon was superseded by a newer logon."));
        _logonTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _logonTcs.Task;
    }

    private void CompleteLogon(Exception? error = null)
    {
        lock (_gate)
        {
            if (error is null)
            {
                _reconnectAttempts = 0;
                _logonTcs?.TrySetResult();
            }
            else
            {
                _logonTcs?.TrySetException(error);
            }
        }
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        try
        {
            string username;
            string password;
            lock (_gate)
            {
                username = _username;
                password = _password;
            }

            if (string.IsNullOrEmpty(username))
            {
                // Logged out while connecting
                return;
            }

            Logger.Information("Connected to Steam! Logging in {UserName}", username);
            var stored = _options.TokenStore.Load();
            // Stores written before AccountName was tracked have an empty name; trust those for the current user
            var storedMatchesUser = stored is not null && (stored.AccountName.Length == 0 ||
                                                           string.Equals(stored.AccountName, username,
                                                               StringComparison.OrdinalIgnoreCase));
            string accountName;
            string refreshToken;
            if (storedMatchesUser && !string.IsNullOrWhiteSpace(stored!.RefreshToken))
            {
                accountName = stored.AccountName.Length > 0 ? stored.AccountName : username;
                refreshToken = stored.RefreshToken;
                if (stored.AccountName.Length == 0)
                {
                    _options.TokenStore.Save(stored with { AccountName = accountName });
                }

                lock (_gate)
                {
                    _usedStoredToken = true;
                }
            }
            else
            {
                var guardData = storedMatchesUser ? stored!.GuardData : null;
                var authSession = await Client.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = username, Password = password, IsPersistentSession = true,
                        GuardData = string.IsNullOrEmpty(guardData) ? null : guardData,
                        Authenticator = _options.Authenticator,
                    });
                Logger.Information("Waiting for logon result...");
                var pollResponse = await authSession.PollingWaitForResultAsync();
                accountName = pollResponse.AccountName;
                refreshToken = pollResponse.RefreshToken;
                _options.TokenStore.Save(new SteamStoredLogin
                {
                    AccountName = accountName,
                    RefreshToken = refreshToken,
                    GuardData = pollResponse.NewGuardData ?? guardData ?? "",
                });
                lock (_gate)
                {
                    _usedStoredToken = false;
                }
            }

            _steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = accountName, AccessToken = refreshToken, ShouldRememberPassword = true,
            });
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error while authenticating with Steam");
            CompleteLogon(e);
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.TryAnotherCM)
        {
            Logger.Warning("Steam requested another CM; reconnecting...");
            Client.Connect();
            return;
        }

        if (callback.Result == EResult.OK)
        {
            Logger.Information("Successfully logged on!");
            CompleteLogon();
            return;
        }

        bool retryWithCredentials;
        lock (_gate)
        {
            retryWithCredentials = _usedStoredToken;
            _usedStoredToken = false;
        }

        if (retryWithCredentials)
        {
            // The stored refresh token was rejected (expired/revoked); forget it and authenticate from scratch
            Logger.Warning("Stored Steam refresh token was rejected ({Result}); re-authenticating with credentials",
                callback.Result);
            _options.TokenStore.Clear();
            Client.Connect();
            return;
        }

        Logger.Error("Unable to logon to Steam: {Result} / {ExtendedResult}", callback.Result,
            callback.ExtendedResult);
        CompleteLogon(new SteamApiException($"Steam login failed: {callback.Result}", callback.Result));
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Logger.Information("Disconnected from Steam (user initiated: {UserInitiated})", callback.UserInitiated);
        if (callback.UserInitiated)
        {
            return;
        }

        int generation;
        int attempt;
        lock (_gate)
        {
            if (_disposed || _logonTcs is null)
            {
                return;
            }

            // Session dropped after a successful logon; anything that awaits the logon now waits for the reconnect
            if (_logonTcs.Task.IsCompletedSuccessfully)
            {
                _logonTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else if (_logonTcs.Task.IsCompleted)
            {
                return;
            }

            generation = _generation;
            attempt = ++_reconnectAttempts;
        }

        if (attempt > _options.MaxReconnectAttempts)
        {
            CompleteLogon(new SteamApiException(
                $"Lost connection to Steam and failed to reconnect after {_options.MaxReconnectAttempts} attempts."));
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Min(_options.MaxReconnectDelay.TotalSeconds,
            Math.Pow(2, attempt)));
        Logger.Warning("Unexpectedly disconnected; reconnecting in {Delay}s (attempt {Attempt}/{Max})",
            delay.TotalSeconds, attempt, _options.MaxReconnectAttempts);
        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            lock (_gate)
            {
                // Logged out, re-logged in, or disposed in the meantime
                if (_disposed || generation != _generation)
                {
                    return;
                }
            }

            Client.Connect();
        }, TaskScheduler.Default);
    }

    private static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        Logger.Information("Logged off of Steam: {Result}", callback.Result);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _logonTcs?.TrySetException(new ObjectDisposedException(nameof(SteamSession)));
        }

        Client.Disconnect();
        _pumpCts.Cancel();
        try
        {
            _pumpTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _pumpCts.Dispose();
    }
}
