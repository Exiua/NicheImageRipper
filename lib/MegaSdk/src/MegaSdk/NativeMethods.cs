using System.Runtime.InteropServices;

namespace MegaSdk;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MegaTransferCallbacks
{
    public nint Context;
    public delegate* unmanaged[Cdecl]<nint, nint, void> OnStart;
    public delegate* unmanaged[Cdecl]<nint, nint, void> OnUpdate;
    public delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> OnTemporaryError;
    public delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> OnFinish;
}

/// <summary>
///     P/Invoke declarations for the flat C ABI in native/include/megasdk_c.h.
/// </summary>
internal static unsafe partial class NativeMethods
{
    private const string LibraryName = "megasdk";

    static NativeMethods()
    {
        NativeLibraryResolver.Register();
    }

    #region Library

    [LibraryImport(LibraryName, EntryPoint = "mega_string_free")]
    internal static partial void StringFree(nint value);

    [LibraryImport(LibraryName, EntryPoint = "mega_set_log_level")]
    internal static partial void SetLogLevel(int logLevel);

    [LibraryImport(LibraryName, EntryPoint = "mega_set_logger")]
    internal static partial void SetLogger(delegate* unmanaged[Cdecl]<byte*, int, byte*, byte*, void> callback);

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_base64_to_handle",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial ulong Base64ToHandle(string base64Handle);

    [LibraryImport(LibraryName, EntryPoint = "mega_handle_to_base64")]
    internal static partial nint HandleToBase64(ulong handle);

    [LibraryImport(LibraryName, EntryPoint = "mega_error_to_string")]
    internal static partial nint ErrorToString(int errorCode);

    #endregion

    #region MegaApi

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_api_create",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint ApiCreate(string appKey, string? basePath, string? userAgent, uint workerThreadCount);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_destroy")]
    internal static partial void ApiDestroy(nint api);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_is_logged_in")]
    internal static partial int ApiIsLoggedIn(nint api);

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_api_login",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ApiLogin(
        nint api,
        string email,
        string password,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_api_login_to_folder",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ApiLoginToFolder(
        nint api,
        string folderLink,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_fetch_nodes")]
    internal static partial void ApiFetchNodes(
        nint api,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_logout")]
    internal static partial void ApiLogout(
        nint api,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_local_logout")]
    internal static partial void ApiLocalLogout(
        nint api,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_api_get_public_node",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ApiGetPublicNode(
        nint api,
        string fileLink,
        delegate* unmanaged[Cdecl]<nint, nint, int, byte*, void> callback,
        nint context);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_get_root_node")]
    internal static partial MegaNodeHandle ApiGetRootNode(nint api);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_get_node_by_handle")]
    internal static partial MegaNodeHandle ApiGetNodeByHandle(nint api, ulong handle);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_get_parent_node")]
    internal static partial MegaNodeHandle ApiGetParentNode(nint api, MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_get_children")]
    internal static partial nint ApiGetChildren(nint api, MegaNodeHandle parent);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_get_node_path")]
    internal static partial nint ApiGetNodePath(nint api, MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_api_authorize_node")]
    internal static partial MegaNodeHandle ApiAuthorizeNode(nint api, MegaNodeHandle node);

    [LibraryImport(
        LibraryName,
        EntryPoint = "mega_api_start_download",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ApiStartDownload(
        nint api,
        MegaNodeHandle node,
        string localPath,
        string? customName,
        nint cancelToken,
        in MegaTransferCallbacks callbacks);

    #endregion

    #region MegaRequest

    [LibraryImport(LibraryName, EntryPoint = "mega_request_get_type")]
    internal static partial int RequestGetType(nint request);

    [LibraryImport(LibraryName, EntryPoint = "mega_request_get_node_handle")]
    internal static partial ulong RequestGetNodeHandle(nint request);

    [LibraryImport(LibraryName, EntryPoint = "mega_request_get_flag")]
    internal static partial int RequestGetFlag(nint request);

    [LibraryImport(LibraryName, EntryPoint = "mega_request_copy_public_node")]
    internal static partial MegaNodeHandle RequestCopyPublicNode(nint request);

    #endregion

    #region MegaTransfer

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_tag")]
    internal static partial int TransferGetTag(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_state")]
    internal static partial int TransferGetState(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_stage")]
    internal static partial int TransferGetStage(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_is_folder")]
    internal static partial int TransferIsFolder(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_folder_transfer_tag")]
    internal static partial int TransferGetFolderTransferTag(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_transferred_bytes")]
    internal static partial long TransferGetTransferredBytes(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_total_bytes")]
    internal static partial long TransferGetTotalBytes(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_speed")]
    internal static partial long TransferGetSpeed(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_node_handle")]
    internal static partial ulong TransferGetNodeHandle(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_file_name")]
    internal static partial nint TransferGetFileName(nint transfer);

    [LibraryImport(LibraryName, EntryPoint = "mega_transfer_get_path")]
    internal static partial nint TransferGetPath(nint transfer);

    #endregion

    #region MegaNode

    [LibraryImport(LibraryName, EntryPoint = "mega_node_free")]
    internal static partial void NodeFree(nint node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_copy")]
    internal static partial MegaNodeHandle NodeCopy(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_type")]
    internal static partial int NodeGetType(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_name")]
    internal static partial nint NodeGetName(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_handle")]
    internal static partial ulong NodeGetHandle(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_parent_handle")]
    internal static partial ulong NodeGetParentHandle(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_size")]
    internal static partial long NodeGetSize(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_creation_time")]
    internal static partial long NodeGetCreationTime(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_modification_time")]
    internal static partial long NodeGetModificationTime(MegaNodeHandle node);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_get_fingerprint")]
    internal static partial nint NodeGetFingerprint(MegaNodeHandle node);

    #endregion

    #region MegaNodeList

    [LibraryImport(LibraryName, EntryPoint = "mega_node_list_free")]
    internal static partial void NodeListFree(nint list);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_list_size")]
    internal static partial int NodeListSize(nint list);

    [LibraryImport(LibraryName, EntryPoint = "mega_node_list_copy_at")]
    internal static partial MegaNodeHandle NodeListCopyAt(nint list, int index);

    #endregion

    #region MegaCancelToken

    [LibraryImport(LibraryName, EntryPoint = "mega_cancel_token_create")]
    internal static partial nint CancelTokenCreate();

    [LibraryImport(LibraryName, EntryPoint = "mega_cancel_token_cancel")]
    internal static partial void CancelTokenCancel(nint token);

    [LibraryImport(LibraryName, EntryPoint = "mega_cancel_token_free")]
    internal static partial void CancelTokenFree(nint token);

    #endregion

    /// <summary>
    ///     Reads a string owned by the native side (valid only while its owner is alive).
    /// </summary>
    internal static string? ReadString(nint value) => value == 0 ? null : Marshal.PtrToStringUTF8(value);

    internal static string? ReadString(byte* value) => ReadString((nint)value);

    /// <summary>
    ///     Reads a string the caller owns, then frees it.
    /// </summary>
    internal static string? TakeString(nint value)
    {
        if (value == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(value);
        }
        finally
        {
            StringFree(value);
        }
    }
}
