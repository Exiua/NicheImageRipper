using System.ComponentModel.DataAnnotations;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Tests;

/// <summary>The checks [ApiController] runs on a PATCH /api/settings body before it reaches the ripper.</summary>
public class ConfigValidationTest
{
    private static List<string> Errors(Config config)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(config, new ValidationContext(config), results, validateAllProperties: true);
        return results.SelectMany(result => result.MemberNames).ToList();
    }

    [Fact]
    public void EmptyUpdateIsValid()
    {
        Assert.Empty(Errors(new Config()));
    }

    [Fact]
    public void SensibleValuesAreValid()
    {
        Assert.Empty(Errors(new Config
        {
            NumThreads = 4,
            MaxRetries = 1,
            RetryDelay = 0,
            SavePath = "Rips",
            FlareSolverrUri = "http://localhost:8191/v1",
            CSWebDriverUri = "",
        }));
    }

    [Theory]
    [InlineData(nameof(Config.NumThreads))]
    [InlineData(nameof(Config.MaxRetries))]
    public void CountsMustBePositive(string property)
    {
        var config = new Config();
        typeof(Config).GetProperty(property)!.SetValue(config, 0);

        Assert.Equal([property], Errors(config));
    }

    [Fact]
    public void RetryDelayCannotBeNegative()
    {
        Assert.Equal([nameof(Config.RetryDelay)], Errors(new Config { RetryDelay = -1 }));
    }

    [Fact]
    public void SavePathCannotBeEmpty()
    {
        Assert.Equal([nameof(Config.SavePath)], Errors(new Config { SavePath = "" }));
    }

    [Theory]
    [InlineData("localhost:8191")]
    [InlineData("ftp://localhost")]
    [InlineData("not a uri")]
    public void ServiceUrisMustBeHttp(string uri)
    {
        Assert.Equal([nameof(Config.FlareSolverrUri), nameof(Config.CSWebDriverUri)],
            Errors(new Config { FlareSolverrUri = uri, CSWebDriverUri = uri }));
    }
}
