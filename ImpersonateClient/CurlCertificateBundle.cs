using System.Reflection;

namespace ImpersonateClient;

internal static class CurlCertificateBundle
{
    private const string ResourceName = "ImpersonateClient.Resources.cacert.pem";

    public static string ResolveCaInfoPath()
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            "ImpersonateClient",
            "cacert.pem");

        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var assembly = typeof(CurlCertificateBundle).Assembly;

        using var resourceStream = assembly.GetManifestResourceStream(ResourceName);

        if (resourceStream is null)
        {
            var availableResources = string.Join(
                Environment.NewLine,
                assembly.GetManifestResourceNames());

            throw new FileNotFoundException(
                $"Embedded CA bundle resource '{ResourceName}' was not found. Available resources:{Environment.NewLine}{availableResources}");
        }

        using var fileStream = File.Create(outputPath);
        resourceStream.CopyTo(fileStream);

        return outputPath;
    }
}