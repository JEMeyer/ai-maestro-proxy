using StackExchange.Redis;
using MySql.Data.MySqlClient;
using ai_maestro_proxy.Services;
using ai_maestro_proxy.Middleware;
using Microsoft.Extensions.Logging.Console;
using ai_maestro_proxy.Logging;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));
builder.Services.AddSingleton<MySqlConnection>(_ => new(builder.Configuration.GetConnectionString("MariaDb")));
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<GpuManagerService>();
builder.Services.AddHttpClient<ProxiedRequestService>();
builder.Services.AddSingleton<HandlerService>();

var app = builder.Build();
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
