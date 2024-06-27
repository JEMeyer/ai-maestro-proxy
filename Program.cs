using StackExchange.Redis;
using MySql.Data.MySqlClient;
using AIMaestroProxy.Services;
using AIMaestroProxy.Middleware;
using Microsoft.Extensions.Logging.Console;
using AIMaestroProxy.Logging;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Load Config values
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Configure services
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddConsole(options => options.FormatterName = "custom");
    loggingBuilder.AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>();
});
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);

// Add Singleton services
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<MySqlConnection>(_ => new(builder.Configuration.GetConnectionString("MariaDb")));
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<GpuManagerService>();
builder.Services.AddSingleton<HandlerService>();

// HttpClient is transient by default
builder.Services.AddHttpClient<ProxiedRequestService>();

// Add SignalR services (SignalR infrastructure is singleton, Hubs are scoped)
builder.Services.AddSignalR();

var app = builder.Build();
app.UseRouting();
app.UseCors();
app.UseMiddleware<TraceIdLoggingMiddleware>();

app.MapPost("/txt2img", async (HttpContext context, HandlerService handlerService) =>
{
    await handlerService.HandleDiffusionRequestAsync(context);
});

app.MapPost("/img2img", async (HttpContext context, HandlerService handlerService) =>
{
    await handlerService.HandleDiffusionRequestAsync(context);
});

app.MapPost("/api/chat", async (HttpContext context, HandlerService handlerService) =>
{
    await handlerService.HandleOllamaRequestAsync(context);
});

app.MapPost("/api/generate", async (HttpContext context, HandlerService handlerService) =>
{
    await handlerService.HandleOllamaRequestAsync(context);
});

app.MapPost("/api/embeddings", async (HttpContext context, HandlerService handlerService) =>
{
    await handlerService.HandleOllamaRequestAsync(context);
});

app.Run();
