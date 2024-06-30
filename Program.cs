using AIMaestroProxy.Endpoints;
using AIMaestroProxy.Handlers;
using AIMaestroProxy.Logging;
using AIMaestroProxy.Middleware;
using AIMaestroProxy.Services;
using Microsoft.Extensions.Logging.Console;
using MySql.Data.MySqlClient;
using StackExchange.Redis;

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
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<OllamaHandler>();
builder.Services.AddSingleton<ComputeHandler>();
builder.Services.AddSingleton<GpuManagerService>();

// HttpClient is transient by default
builder.Services.AddHttpClient<ProxiedRequestService>();

var app = builder.Build();

// Middleware to handle errors globally
app.UseMiddleware<ErrorHandlingMiddleware>();

// Middleware to log request trace ID and handle stopwatch
app.UseMiddleware<TraceIdLoggingMiddleware>();
app.UseMiddleware<StopwatchMiddleware>();

// Middleware to remove chunked transfer encoding header
app.UseMiddleware<RemoveChunkedTransferEncodingMiddleware>();

// Middleware to handle not found responses
app.UseMiddleware<NotFoundLoggingMiddleware>();

// Ollama, StableDiffusion, Coqui, and Whisper endpoints
app.MapOllamaEndpoints();
app.MapDiffusionEndpoints();


app.Run();
