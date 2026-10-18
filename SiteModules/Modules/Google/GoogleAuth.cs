using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.SiteModules.Modules.Google;

internal static class GoogleAuth
{
    public static async Task<UserCredential> GDriveAuthenticate()
    {
        await using var stream = AppData.OpenRead("client_secrets.json");
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            [DriveService.Scope.DriveReadonly],
            "user", CancellationToken.None, new FileDataStore("GDriveCredentialCache"));

        return credential;
    }
}