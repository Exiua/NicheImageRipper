using Service.Models;

namespace Service.Utilities.ExtensionMethods;

public static class CoreConfigExtensionMethods
{
    public static void UpdateCredentials(this NicheImageRipper.Core.Configuration.Credentials credentials, Credentials? newCredentials)
    {
        if (newCredentials is null)
        {
            return;
        }

        if (newCredentials.Username is not null)
        {
            credentials.Username = newCredentials.Username;
        }

        if (newCredentials.Password is not null)
        {
            credentials.Password = newCredentials.Password;
        }
    }
}