namespace NicheImageRipper.Core.FileDownloading;

internal static class MegaSessionManager
{
    private static bool? _loggedIn;

    public static bool EnsureLoggedIn(string email, string password)
    {
        if (_loggedIn == true)
        {
            return true;
        }

        _loggedIn = MegaApi.WhoAmI() == email || MegaApi.Login(email, password);
        return _loggedIn.Value;
    }
}