using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;

namespace NicheImageRipper.SiteModules.Modules.Google;

internal static class GoogleAuth
{
    public static async Task<UserCredential> GDriveAuthenticate()
    {
        await using var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            [DriveService.Scope.DriveReadonly],
            "user", CancellationToken.None, new FileDataStore("GDriveCredentialCache"));

        return credential;
    }
}