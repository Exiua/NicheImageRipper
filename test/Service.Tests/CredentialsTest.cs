using NicheImageRipper.Service.Models;
using NicheImageRipper.Service.Utilities.ExtensionMethods;
using CoreCredentials = NicheImageRipper.Sdk.Configuration.Credentials;

namespace NicheImageRipper.Service.Tests;

public class CredentialsTest
{
    private static CoreCredentials Existing() => new() { Username = "old-user", Password = "old-pass" };

    [Fact]
    public void NullUpdateChangesNothing()
    {
        var credentials = Existing();

        credentials.UpdateCredentials(null);

        Assert.Equal(("old-user", "old-pass"), (credentials.Username, credentials.Password));
    }

    [Fact]
    public void OnlyGivenFieldsAreReplaced()
    {
        var credentials = Existing();

        credentials.UpdateCredentials(new Credentials { Password = "new-pass" });

        Assert.Equal(("old-user", "new-pass"), (credentials.Username, credentials.Password));
    }

    [Fact]
    public void EmptyStringClearsAField()
    {
        // Null means "leave it"; an empty string is a real value the client asked for
        var credentials = Existing();

        credentials.UpdateCredentials(new Credentials { Username = "", Password = "new-pass" });

        Assert.Equal(("", "new-pass"), (credentials.Username, credentials.Password));
    }

    [Fact]
    public void ConversionCopiesBothFields()
    {
        var converted = Credentials.FromCoreCredentials(Existing());

        Assert.Equal(("old-user", "old-pass"), (converted.Username, converted.Password));
    }
}
