using AIMaestroProxy.Logging;
using AIMaestroProxy.Middleware;
using AIMaestroProxy.Models;
using AIMaestroProxy.Services;
using Microsoft.Extensions.Logging.Console;
using MySql.Data.MySqlClient;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

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

// Add Singletone services for the database/redis clients
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<MySqlConnection>(_ => new(builder.Configuration.GetConnectionString("MariaDb")));

// Add Singleton services for Services
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<GpuManagerService>();
builder.Services.AddControllers();

// Load up endpoints from config. Dynamic for idk why, someone can use this with any backend.
builder.Services.Configure<PathCategories>(builder.Configuration.GetSection("PathCategories"));

// Add transient HttpClient service for proxied requests
builder.Services.AddHttpClient<ProxiedRequestService>();
builder.Services.AddHttpClient<ProxiedRequestService2>();

var app = builder.Build();

// Middleware to handle errors globally
app.UseMiddleware<ErrorHandlingMiddleware>();

// Middleware to log request trace ID and handle stopwatch
app.UseMiddleware<TraceIdLoggingMiddleware>();
app.UseMiddleware<StopwatchMiddleware>();

// Middleware to handle not found responses
app.UseMiddleware<NotFoundLoggingMiddleware>();

// Ollama, StableDiffusion, Coqui, and Whisper endpoints
// app.MapOllamaEndpoints();
// app.MapDiffusionEndpoints();
// app.MapCoquiEndpoints();
// app.MapIFWhisperEndpoints();

app.MapControllerRoute(
    name: "default",
    pattern: "{*path}",
    defaults: new { controller = "Proxy", action = "HandleRequest" });

app.Run();
