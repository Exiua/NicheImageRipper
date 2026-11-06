using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MegaSdk;

/// <summary>
///     A MEGA session (wraps <c>MegaApi</c>). A client is either anonymous, logged in to an account, or logged in to a
///     folder link. Public file links can be resolved with <see cref="GetPublicNodeAsync" /> in any of these states.
/// </summary>
public sealed class MegaClient : IDisposable
{
    private readonly ConcurrentDictionary<nint, NativeOperation> _pending = new();
    private readonly Lock _lock = new();
    private nint _api;

    public MegaClient(MegaClientOptions options)
    {
        _api = NativeMethods.ApiCreate(options.AppKey, options.BasePath, options.UserAgent,
            options.WorkerThreadCount);

        if (_api == 0)
        {
            throw new InvalidOperationException("mega_api_create failed.");
        }
    }

    public bool IsLoggedIn => NativeMethods.ApiIsLoggedIn(Api) != 0;

    private nint Api
    {
        get
        {
            var api = _api;
            ObjectDisposedException.ThrowIf(api == 0, this);
            return api;
        }
    }

    #region Requests

    /// <summary>
    ///     Logs in to an account. Call <see cref="FetchNodesAsync" /> afterwards to load its nodes.
    /// </summary>
    public unsafe Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return StartRequest(false, cancellationToken,
            (api, callback, context) => NativeMethods.ApiLogin(api, email, password, callback, context));
    }

    /// <summary>
    ///     Logs in to a folder link (https://mega.nz/folder/...). Call <see cref="FetchNodesAsync" /> afterwards to
    ///     load its nodes; <see cref="GetRootNode" /> is then the shared folder.
    /// </summary>
    public unsafe Task LoginToFolderAsync(string folderLink, CancellationToken cancellationToken = default)
    {
        return StartRequest(false, cancellationToken,
            (api, callback, context) => NativeMethods.ApiLoginToFolder(api, folderLink, callback, context));
    }

    public unsafe Task FetchNodesAsync(CancellationToken cancellationToken = default)
    {
        return StartRequest(false, cancellationToken,
            (api, callback, context) => NativeMethods.ApiFetchNodes(api, callback, context));
    }

    /// <summary>
    ///     Logs in to a folder link and fetches its nodes.
    /// </summary>
    public async Task<MegaNode> OpenFolderLinkAsync(string folderLink, CancellationToken cancellationToken = default)
    {
        await LoginToFolderAsync(folderLink, cancellationToken).ConfigureAwait(false);
        await FetchNodesAsync(cancellationToken).ConfigureAwait(false);
        return GetRootNode() ?? throw new MegaException(MegaErrorCode.NotFound, "Folder link has no root node.");
    }

    /// <summary>
    ///     Ends the session. For folder link sessions this only ends the local session.
    /// </summary>
    public unsafe Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return StartRequest(false, cancellationToken,
            (api, callback, context) => NativeMethods.ApiLogout(api, callback, context));
    }

    /// <summary>
    ///     Ends the local session without invalidating it on the server.
    /// </summary>
    public unsafe Task LocalLogoutAsync(CancellationToken cancellationToken = default)
    {
        return StartRequest(false, cancellationToken,
            (api, callback, context) => NativeMethods.ApiLocalLogout(api, callback, context));
    }

    /// <summary>
    ///     Resolves a public file link (https://mega.nz/file/...). The node can be downloaded without logging in.
    /// </summary>
    public async Task<MegaNode> GetPublicNodeAsync(string fileLink, CancellationToken cancellationToken = default)
    {
        var node = await StartGetPublicNode(fileLink, cancellationToken).ConfigureAwait(false);
        return node ?? throw new MegaException(MegaErrorCode.NotFound, "Public link returned no node.");
    }

    private unsafe Task<MegaNode?> StartGetPublicNode(string fileLink, CancellationToken cancellationToken)
    {
        return StartRequest(true, cancellationToken,
            (api, callback, context) => NativeMethods.ApiGetPublicNode(api, fileLink, callback, context));
    }

    private unsafe delegate void RequestStarter(
        nint api,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    private unsafe Task<MegaNode?> StartRequest(bool returnsPublicNode, CancellationToken cancellationToken,
                                         RequestStarter start)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var operation = new RequestOperation(this, returnsPublicNode);
        var context = Register(operation);

        try
        {
            lock (_lock)
            {
                start(Api, &OnRequestFinish, context);
            }
        }
        catch
        {
            Unregister(context)?.Fail(null);
            throw;
        }

        // The SDK can't cancel requests; this only abandons the wait.
        operation.CancellationRegistration = cancellationToken.Register(
            static state => ((RequestOperation)state!).Completion.TrySetCanceled(), operation);
        if (operation.Completion.Task.IsCompleted)
        {
            // Finished before the registration was attached.
            operation.CancellationRegistration.Dispose();
        }

        return operation.Completion.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnRequestFinish(nint context, nint request, int errorCode, byte* errorString)
    {
        if (GetOperation(context) is not RequestOperation operation || operation.Owner.Unregister(context) is null)
        {
            return;
        }

        try
        {
            operation.CancellationRegistration.Dispose();

            if (errorCode != (int)MegaErrorCode.Ok)
            {
                operation.Completion.TrySetException(new MegaException((MegaErrorCode)errorCode,
                    NativeMethods.ReadString(errorString)));
                return;
            }

            MegaNode? node = null;
            if (operation.ReturnsPublicNode && request != 0)
            {
                node = MegaNode.FromNative(NativeMethods.RequestCopyPublicNode(request));
            }

            if (!operation.Completion.TrySetResult(node))
            {
                node?.Dispose();
            }
        }
        catch (Exception e)
        {
            // Exceptions can't propagate into native code.
            operation.Completion.TrySetException(e);
        }
    }

    #endregion

    #region Nodes

    /// <summary>
    ///     Root of the account's cloud drive, or of the folder for folder link sessions. Null until nodes are fetched.
    /// </summary>
    public MegaNode? GetRootNode()
    {
        lock (_lock)
        {
            return MegaNode.FromNative(NativeMethods.ApiGetRootNode(Api));
        }
    }

    public MegaNode? GetNodeByHandle(ulong handle)
    {
        lock (_lock)
        {
            return MegaNode.FromNative(NativeMethods.ApiGetNodeByHandle(Api, handle));
        }
    }

    public MegaNode? GetNodeByHandle(string base64Handle) => GetNodeByHandle(Base64ToHandle(base64Handle));

    public MegaNode? GetParentNode(MegaNode node)
    {
        lock (_lock)
        {
            return MegaNode.FromNative(NativeMethods.ApiGetParentNode(Api, node.Native));
        }
    }

    public IReadOnlyList<MegaNode> GetChildren(MegaNode parent)
    {
        nint list;
        lock (_lock)
        {
            list = NativeMethods.ApiGetChildren(Api, parent.Native);
        }

        if (list == 0)
        {
            return [];
        }

        try
        {
            var size = NativeMethods.NodeListSize(list);
            var children = new List<MegaNode>(size);
            for (var i = 0; i < size; i++)
            {
                if (MegaNode.FromNative(NativeMethods.NodeListCopyAt(list, i)) is { } child)
                {
                    children.Add(child);
                }
            }

            return children;
        }
        finally
        {
            NativeMethods.NodeListFree(list);
        }
    }

    /// <summary>
    ///     All descendants of <paramref name="parent" /> (depth first), with their path relative to it.
    /// </summary>
    public IEnumerable<(string RelativePath, MegaNode Node)> EnumerateDescendants(MegaNode parent)
    {
        var stack = new Stack<(string, MegaNode)>();
        stack.Push(("", parent));

        while (stack.Count > 0)
        {
            var (path, node) = stack.Pop();
            foreach (var child in GetChildren(node).Reverse())
            {
                var childPath = path.Length == 0 ? child.Name : $"{path}/{child.Name}";
                if (child.IsFolder)
                {
                    stack.Push((childPath, child));
                }

                yield return (childPath, child);
            }
        }
    }

    public string? GetNodePath(MegaNode node)
    {
        lock (_lock)
        {
            return NativeMethods.TakeString(NativeMethods.ApiGetNodePath(Api, node.Native));
        }
    }

    /// <summary>
    ///     Returns a copy of a node from this folder link session that other sessions can download.
    /// </summary>
    public MegaNode? AuthorizeNode(MegaNode node)
    {
        lock (_lock)
        {
            return MegaNode.FromNative(NativeMethods.ApiAuthorizeNode(Api, node.Native));
        }
    }

    #endregion

    #region Transfers

    /// <summary>
    ///     Downloads a file or folder.
    /// </summary>
    /// <param name="node">Node to download.</param>
    /// <param name="destination">
    ///     A directory ending in a path separator (the node's name is used), or a full path for the file or folder.
    ///     If a file with the same name but different contents exists, the download is saved as "name (1).ext".
    /// </param>
    /// <param name="progress">Receives progress for the whole transfer (for folders, all files in it).</param>
    public unsafe Task DownloadAsync(MegaNode node, string destination, IProgress<MegaTransferProgress>? progress = null,
                              CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The SDK requires an absolute path. Path.GetFullPath keeps a trailing separator.
        var localPath = Path.GetFullPath(destination);
        var operation = new TransferOperation(this, progress, NativeMethods.CancelTokenCreate());
        var context = Register(operation);

        var callbacks = new MegaTransferCallbacks
        {
            Context = context,
            OnStart = &OnTransferUpdate,
            OnUpdate = &OnTransferUpdate,
            OnTemporaryError = null,
            OnFinish = &OnTransferFinish,
        };

        try
        {
            lock (_lock)
            {
                NativeMethods.ApiStartDownload(Api, node.Native, localPath, null, operation.CancelToken,
                    in callbacks);
            }
        }
        catch
        {
            Unregister(context)?.Fail(null);
            throw;
        }

        operation.CancellationRegistration = cancellationToken.Register(
            static state => ((TransferOperation)state!).Cancel(), operation);
        if (operation.Completion.Task.IsCompleted)
        {
            // Finished before the registration was attached.
            operation.CancellationRegistration.Dispose();
        }

        return operation.Completion.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnTransferUpdate(nint context, nint transfer)
    {
        try
        {
            if (GetOperation(context) is not TransferOperation { Progress: { } progress } ||
                NativeMethods.TransferGetFolderTransferTag(transfer) > 0)
            {
                return;
            }

            progress.Report(ReadProgress(transfer));
        }
        catch
        {
            // Progress reporting must not take down the SDK thread.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnTransferFinish(nint context, nint transfer, int errorCode, byte* errorString)
    {
        if (GetOperation(context) is not TransferOperation operation || operation.Owner.Unregister(context) is null)
        {
            return;
        }

        try
        {
            operation.Release();

            var state = transfer == 0
                ? MegaTransferState.None
                : (MegaTransferState)NativeMethods.TransferGetState(transfer);

            if (errorCode == (int)MegaErrorCode.Ok && state != MegaTransferState.Cancelled)
            {
                if (transfer != 0)
                {
                    operation.Progress?.Report(ReadProgress(transfer));
                }

                operation.Completion.TrySetResult();
            }
            else if (operation.CancellationRequested || state == MegaTransferState.Cancelled)
            {
                operation.Completion.TrySetCanceled();
            }
            else
            {
                operation.Completion.TrySetException(new MegaException((MegaErrorCode)errorCode,
                    NativeMethods.ReadString(errorString)));
            }
        }
        catch (Exception e)
        {
            operation.Completion.TrySetException(e);
        }
    }

    private static MegaTransferProgress ReadProgress(nint transfer)
    {
        return new MegaTransferProgress(
            NativeMethods.ReadString(NativeMethods.TransferGetFileName(transfer)),
            NativeMethods.TransferGetTransferredBytes(transfer),
            NativeMethods.TransferGetTotalBytes(transfer),
            NativeMethods.TransferGetSpeed(transfer),
            (MegaTransferState)NativeMethods.TransferGetState(transfer));
    }

    #endregion

    #region Utilities

    public static ulong Base64ToHandle(string base64Handle) => NativeMethods.Base64ToHandle(base64Handle);

    public static string HandleToBase64(ulong handle) =>
        NativeMethods.TakeString(NativeMethods.HandleToBase64(handle)) ?? "";

    public static string GetErrorString(MegaErrorCode errorCode) =>
        NativeMethods.ReadString(NativeMethods.ErrorToString((int)errorCode)) ?? errorCode.ToString();

    #endregion

    #region Operation tracking

    private nint Register(NativeOperation operation)
    {
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(operation));
        _pending[context] = operation;
        return context;
    }

    /// <summary>
    ///     Removes and frees the operation's handle. Returns null if it was already removed (finished, or failed by
    ///     <see cref="Dispose" />), so exactly one caller completes each operation.
    /// </summary>
    private NativeOperation? Unregister(nint context)
    {
        if (!_pending.TryRemove(context, out var operation))
        {
            return null;
        }

        GCHandle.FromIntPtr(context).Free();
        return operation;
    }

    /// <summary>
    ///     Handles are only freed by the operation's final callback or, after the native api is destroyed (so no
    ///     callbacks remain), by <see cref="Dispose" />. Callbacks can therefore always resolve their handle.
    /// </summary>
    private static NativeOperation? GetOperation(nint context) =>
        GCHandle.FromIntPtr(context).Target as NativeOperation;

    private abstract class NativeOperation(MegaClient owner)
    {
        public MegaClient Owner { get; } = owner;
        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public abstract void Fail(Exception? exception);
    }

    private sealed class RequestOperation(MegaClient owner, bool returnsPublicNode) : NativeOperation(owner)
    {
        public bool ReturnsPublicNode { get; } = returnsPublicNode;

        public TaskCompletionSource<MegaNode?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Fail(Exception? exception)
        {
            CancellationRegistration.Dispose();
            if (exception is null)
            {
                Completion.TrySetCanceled();
            }
            else
            {
                Completion.TrySetException(exception);
            }
        }
    }

    private sealed class TransferOperation(
        MegaClient owner,
        IProgress<MegaTransferProgress>? progress,
        nint cancelToken) : NativeOperation(owner)
    {
        private nint _cancelToken = cancelToken;

        public IProgress<MegaTransferProgress>? Progress { get; } = progress;
        public nint CancelToken => _cancelToken;
        public bool CancellationRequested { get; private set; }

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Cancel()
        {
            CancellationRequested = true;
            var token = _cancelToken;
            if (token != 0)
            {
                NativeMethods.CancelTokenCancel(token);
            }
        }

        /// <summary>
        ///     Frees the native cancel token. The SDK no longer uses it once the transfer has finished.
        /// </summary>
        public void Release()
        {
            // Waits for an in-flight Cancel, so the token isn't freed under it.
            CancellationRegistration.Dispose();

            var token = Interlocked.Exchange(ref _cancelToken, 0);
            if (token != 0)
            {
                NativeMethods.CancelTokenFree(token);
            }
        }

        public override void Fail(Exception? exception)
        {
            Release();
            if (exception is null)
            {
                Completion.TrySetCanceled();
            }
            else
            {
                Completion.TrySetException(exception);
            }
        }
    }

    #endregion

    public void Dispose()
    {
        nint api;
        lock (_lock)
        {
            api = _api;
            _api = 0;
        }

        if (api == 0)
        {
            return;
        }

        // Callbacks may run while the SDK shuts down, but none run after this returns.
        NativeMethods.ApiDestroy(api);

        foreach (var context in _pending.Keys)
        {
            Unregister(context)?.Fail(new ObjectDisposedException(nameof(MegaClient)));
        }
    }
}
