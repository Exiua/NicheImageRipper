using Microsoft.EntityFrameworkCore;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.Service.Contexts;

namespace NicheImageRipper.Service.Configuration;

/// <summary>Where the API key database lives. Shared by the Service and ServiceAdminCli so both open the same file.</summary>
public static class ServiceDatabase
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    ///     <c>ConnectionStrings:DefaultConnection</c> if set, otherwise <c>NicheImageRipper.db</c> in the data folder
    ///     (<see cref="AppData"/>), the same database Core keeps its history in.
    /// </summary>
    public static string GetConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(configured)
            ? $"Data Source={AppData.GetPathForWrite("NicheImageRipper.db")}"
            : configured;
    }

    public static IServiceCollection AddServiceDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);
        return services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
    }

    /// <summary>Creates the database or applies pending migrations.</summary>
    public static async Task MigrateServiceDatabaseAsync(this IServiceProvider services,
                                                         CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
