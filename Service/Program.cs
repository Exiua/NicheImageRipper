using Service.Contexts;
using Service.Handlers;
using Service.Serilog;
using Service.Services;
using Service.Singletons;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);


#if DEBUG
const LogEventLevel minimumLevel = LogEventLevel.Debug;
#else
const LogEventLevel minimumLevel = LogEventLevel.Information;
#endif

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                 "logs/service-.log",
                 rollingInterval: RollingInterval.Day,
                 retainedFileCountLimit: 14,
                 shared: true)
            .CreateLogger();


builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add services to the container.
builder.Services.AddAuthorization();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var broadcaster = new WebSocketLogBroadcaster(null);
builder.Services.AddSingleton(broadcaster);
builder.Services.AddSingleton<INicheImageRipperSingleton, NicheImageRipperSingleton>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services
       .AddAuthentication("ApiKey")
       .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration
       .WriteTo.Console()
       .WriteTo.WebSocketLogs(broadcaster);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseWebSockets();
app.MapControllers();

app.Map("/ws/logs", async context =>
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var broadcaster = context.RequestServices.GetRequiredService<WebSocketLogBroadcaster>();
        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        await broadcaster.AddClientAndWaitAsync(socket, context.RequestAborted);
    })
   .RequireAuthorization(new AuthorizeAttribute
    {
        AuthenticationSchemes = "ApiKey"
    });

app.Run();