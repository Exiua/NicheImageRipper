using CoreService.Contexts;
using CoreService.Handlers;
using CoreService.Services;
using CoreService.Singletons;
using Microsoft.AspNetCore.Authentication;
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

builder.Host.UseSerilog();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add services to the container.
builder.Services.AddAuthorization();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<INicheImageRipperSingleton, NicheImageRipperSingleton>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services
       .AddAuthentication("ApiKey")
       .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();