using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Service.Contexts;
using NicheImageRipper.Service.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

using var host = builder.Build();

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant();

if (string.IsNullOrWhiteSpace(command))
{
    PrintUsage();
    return;
}

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var logger = services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("AdminCli");
var apiKeyService = services.GetRequiredService<IApiKeyService>();

switch (command)
{
    case "gen":
    {
        var name = args.Length > 1 ? args[1] : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Usage: admincli gen <name>");
            return;
        }

        var result = await apiKeyService.CreateAsync(
            name: name,
            ownerId: null,
            expiresUtc: null);

        Console.WriteLine("API key created. Store it now.");
        Console.WriteLine($"Id:     {result.Id}");
        Console.WriteLine($"Name:   {result.Name}");
        Console.WriteLine($"Prefix: {result.Prefix}");
        Console.WriteLine($"Key:    {result.RawKey}");
        break;
    }

    case "revoke":
    {
        if (args.Length < 2 || !Guid.TryParse(args[1], out var id))
        {
            Console.WriteLine("Usage: admincli revoke <guid>");
            return;
        }

        var success = await apiKeyService.RevokeAsync(id);
        Console.WriteLine(success ? "Revoked." : "Key not found.");
        break;
    }

    case "list":
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        var keys = await db.ApiKeys
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.KeyPrefix,
                x.IsRevoked,
                x.CreatedUtc,
                x.ExpiresUtc,
                x.LastUsedUtc
            })
            .ToListAsync();

        foreach (var key in keys)
        {
            Console.WriteLine($"{key.Id}  {key.Name}  {key.KeyPrefix}  revoked={key.IsRevoked}");
        }

        break;
    }

    default:
    {
        PrintUsage();
        break;
    }
}

return;

static void PrintUsage()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  admincli gen <name>");
    Console.WriteLine("  admincli revoke <guid>");
    Console.WriteLine("  admincli list");
}