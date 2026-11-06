using MegaSdk;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Features;
using NicheImageRipper.Sdk.Utility;
using Serilog;

namespace NicheImageRipper.SiteModules.Modules.Mega;

/// <summary>
///     Owns the MEGA SDK sessions for the process:
///     <list type="bullet">
///         <item>
///             One shared client that does all downloads. It's logged in to the account in the <c>mega</c> site
///             config if one is set (for that account's transfer quota), otherwise anonymous.
///         </item>
///         <item>
///             A small cache of folder link sessions, used to list folders and to authorize their files. An
///             authorized node can be downloaded by any client, so folder sessions are never used for transfers.
///         </item>
///     </list>
/// </summary>
internal static class MegaClientProvider
{
    private const int MaxFolderSessions = 4;

    private static readonly ILogger Logger = Log.ForContext(typeof(MegaClientProvider));
    private static readonly SemaphoreSlim SharedLock = new(1, 1);
    private static readonly Lock FolderSessionsLock = new();
    private static readonly LinkedList<FolderSession> FolderSessions = []; // Most recently used first

    private static MegaClient? _shared;

    static MegaClientProvider()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeAll();
    }

    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;

    /// <summary>
    ///     Throws <see cref="FeatureNotAvailableException" /> if the native library isn't built/deployed.
    /// </summary>
    public static void EnsureAvailable()
    {
        if (!AvailableFeatureManager.AvailableFeatures.HasFeature(FeatureKeys.MegaSdk,
                MegaClient.IsNativeLibraryAvailable))
        {
            throw new FeatureNotAvailableException(FeatureKeys.MegaSdk);
        }
    }

    /// <summary>
    ///     The client that does all downloads, created (and logged in, if configured) on first use.
    /// </summary>
    public static async Task<MegaClient> GetSharedClientAsync(CancellationToken cancellationToken = default)
    {
        if (_shared is { } shared)
        {
            return shared;
        }

        await SharedLock.WaitAsync(cancellationToken);
        try
        {
            if (_shared is not null)
            {
                return _shared;
            }

            var client = CreateClient("shared");
            var (email, password) = Config.GetSiteConfig(MegaParser.ParserName)?.Login.Deconstruct() ?? (null, null);
            if (!email.IsNullOrWhiteSpace() && !password.IsNullOrWhiteSpace())
            {
                Logger.Debug("Logging in to MEGA as {Email}", email);
                try
                {
                    await client.LoginAsync(email, password, cancellationToken);
                }
                catch (MegaException e)
                {
                    client.Dispose();
                    throw new RipperException($"Unable to log in to MEGA: {e.Message}", e);
                }
            }
            else
            {
                Logger.Debug("No MEGA login configured; downloading anonymously");
            }

            _shared = client;
            return client;
        }
        finally
        {
            SharedLock.Release();
        }
    }

    /// <summary>
    ///     Runs <paramref name="action" /> against the folder link's session (created and cached on first use). Only
    ///     one action runs per session at a time, and the session isn't disposed while it runs.
    /// </summary>
    public static async Task<T> WithFolderSessionAsync<T>(string folderLink,
                                                          Func<MegaClient, MegaNode, T> action,
                                                          CancellationToken cancellationToken = default)
    {
        var session = GetOrAddFolderSession(folderLink);
        await session.Lock.WaitAsync(cancellationToken);
        try
        {
            var (client, root) = await session.OpenAsync(cancellationToken);
            return action(client, root);
        }
        catch (MegaException)
        {
            // Don't keep a failed login/fetch around; the next caller opens the folder again
            session.Dispose();
            throw;
        }
        finally
        {
            if (session.Evicted)
            {
                session.Dispose();
            }

            session.Lock.Release();
        }
    }

    private static MegaClient CreateClient(params string[] cacheDirectory)
    {
        return new MegaClient(new MegaClientOptions
        {
            BasePath = AppData.EnsureDirectory(["MegaSdk", ..cacheDirectory]),
        });
    }

    private static FolderSession GetOrAddFolderSession(string folderLink)
    {
        FolderSession? evicted = null;
        FolderSession session;
        lock (FolderSessionsLock)
        {
            var node = FolderSessions.First;
            while (node is not null && node.Value.FolderLink != folderLink)
            {
                node = node.Next;
            }

            if (node is not null)
            {
                FolderSessions.Remove(node);
                FolderSessions.AddFirst(node);
                return node.Value;
            }

            session = new FolderSession(folderLink);
            FolderSessions.AddFirst(session);
            if (FolderSessions.Count > MaxFolderSessions)
            {
                evicted = FolderSessions.Last!.Value;
                FolderSessions.RemoveLast();
            }
        }

        evicted?.Evict();
        return session;
    }

    private static void DisposeAll()
    {
        List<FolderSession> sessions;
        lock (FolderSessionsLock)
        {
            sessions = [..FolderSessions];
            FolderSessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }

        Interlocked.Exchange(ref _shared, null)?.Dispose();
    }

    private sealed class FolderSession(string folderLink) : IDisposable
    {
        private MegaClient? _client;
        private MegaNode? _root;

        public string FolderLink { get; } = folderLink;
        public SemaphoreSlim Lock { get; } = new(1, 1);

        /// <summary>
        ///     Removed from the cache. Whoever holds <see cref="Lock" /> disposes it before releasing.
        /// </summary>
        public bool Evicted { get; private set; }

        /// <summary>
        ///     Logs in to the folder link and fetches its nodes on first use. Must be called holding <see cref="Lock" />.
        /// </summary>
        public async Task<(MegaClient, MegaNode)> OpenAsync(CancellationToken cancellationToken)
        {
            if (_client is not null && _root is not null)
            {
                return (_client, _root);
            }

            Logger.Debug("Opening MEGA folder link {FolderLink}", FolderLink);
            _client = CreateClient("folders", StringUtility.HashStringMd5(FolderLink));
            _root = await _client.OpenFolderLinkAsync(FolderLink, cancellationToken);
            return (_client, _root);
        }

        public void Evict()
        {
            Evicted = true;
            _ = Task.Run(async () =>
            {
                await Lock.WaitAsync();
                try
                {
                    Dispose();
                }
                finally
                {
                    Lock.Release();
                }
            });
        }

        public void Dispose()
        {
            _root?.Dispose();
            _root = null;
            _client?.Dispose();
            _client = null;
        }
    }
}
