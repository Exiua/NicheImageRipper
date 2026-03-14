using CoreService.Singletons;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);


#if DEBUG
const LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Debug;
#else
const LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information;
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

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<INicheImageRipperSingleton, NicheImageRipperSingleton>();

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