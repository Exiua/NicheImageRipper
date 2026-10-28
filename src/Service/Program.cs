using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.Service.Configuration;
using NicheImageRipper.Service.Handlers;
using NicheImageRipper.Service.Models.WebSocket;
using NicheImageRipper.Service.Serilog;
using NicheImageRipper.Service.Services;
using NicheImageRipper.Service.Singletons;
using Serilog;
using Serilog.Events;

// Console only until the host's logger replaces it; catches errors from building the host
Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var loggingOptions = builder.Configuration.GetSection(ServiceLoggingOptions.SectionName)
                                .Get<ServiceLoggingOptions>() ?? new ServiceLoggingOptions();

    builder.Services.AddServiceDatabase(builder.Configuration);

    // Add services to the container.
    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddSingleton(loggingOptions);
    builder.Services.AddSingleton<WebSocketBroadcaster>();
    builder.Services.AddSingleton<INicheImageRipperSingleton, NicheImageRipperSingleton>();
    builder.Services.AddHostedService<RipperLifetimeService>();
    builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

    builder.Services
           .AddAuthentication("ApiKey")
           .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

    builder.Host.UseSerilog((_, services, configuration) =>
    {
        configuration
           .MinimumLevel.Is(loggingOptions.PipelineMinimumLevel)
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
           .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
           .MinimumLevel.Override("System", LogEventLevel.Warning)
           // Logs every request at Debug, which buries everything else while a client polls
           .MinimumLevel.Override(typeof(ApiKeyAuthenticationHandler).FullName!, LogEventLevel.Information)
           .Enrich.FromLogContext()
           .WriteTo.Console(restrictedToMinimumLevel: loggingOptions.MinimumLevel)
           .WriteTo.File(
                AppData.GetPath("Logs", "service-.log"),
                restrictedToMinimumLevel: loggingOptions.MinimumLevel,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
           .WriteTo.WebSocketLogs(() => services.GetRequiredService<WebSocketBroadcaster>());
    });

    var app = builder.Build();

    await app.Services.MigrateServiceDatabaseAsync();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseWebSockets();
    app.MapControllers();

    // Clients pick the log events they want with ?logLevel=<level> and can change it later by sending a
    // WsClientMessage of type SetLogLevel
    app.Map("/ws/events", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            LogEventLevel? logLevel = null;
            if (context.Request.Query.TryGetValue("logLevel", out var logLevelValue))
            {
                if (!Enum.TryParse<WsLogLevel>(logLevelValue, ignoreCase: true, out var parsed) ||
                    !Enum.IsDefined(parsed))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync($"Unknown logLevel '{logLevelValue}'.");
                    return;
                }

                logLevel = (LogEventLevel)parsed;
            }

            var broadcaster = context.RequestServices.GetRequiredService<WebSocketBroadcaster>();
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            await broadcaster.AddClientAndWaitAsync(socket, logLevel, context.RequestAborted);
        })
       .RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = "ApiKey"
        });

    app.Lifetime.ApplicationStarted.Register(() => WarnAboutPlainHttp(app));

    await app.RunAsync();
    return 0;
}
catch (Exception e) when (e is not HostAbortedException) // HostAbortedException is how `dotnet ef` stops the host
{
    Log.Fatal(e, "The service stopped unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// API keys, settings and site credentials travel in every request, so plain HTTP is only safe on this machine
static void WarnAboutPlainHttp(WebApplication app)
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    foreach (var address in addresses)
    {
        if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        // Wildcard bindings (http://*:5108, http://+:5108) don't parse as URIs and listen on every interface
        var isLoopback = Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
                         (uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip)));
        if (!isLoopback)
        {
            app.Logger.LogWarning("Listening on {Address} over plain HTTP. API keys and site credentials are sent " +
                                  "unencrypted; use HTTPS or a TLS-terminating reverse proxy when the service is " +
                                  "reachable from other machines.", address);
        }
    }
}
