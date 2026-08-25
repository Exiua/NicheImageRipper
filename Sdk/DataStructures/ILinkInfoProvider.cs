namespace Sdk.DataStructures;

/// <summary>
///     Marker for classes that declare LinkInfo values (e.g. MegaLinkInfo.Mega). Implementing this interface
///     ensures the class's static constructor — and therefore its LinkInfoRegistry.Register calls — runs
///     during startup discovery, before anything might try to deserialize that LinkInfo from storage.
/// </summary>
public interface ILinkInfoProvider;