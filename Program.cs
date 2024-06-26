using StackExchange.Redis;
using MySql.Data.MySqlClient;
using ai_maestro_proxy.Models;
using ai_maestro_proxy.Services;
using ai_maestro_proxy.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load Config values
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Configure services
builder.Services.AddLogging();
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? ""));
builder.Services.AddSingleton<MySqlConnection>(_ => new(builder.Configuration.GetConnectionString("MariaDb")));
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<GpuManagerService>();
builder.Services.AddHttpClient<ProxiedRequestService>();
builder.Services.AddSingleton<HandlerService>();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapPost("/txt2img", async (HttpContext context, HandlerService handlerService) =>
{
    var request = await context.Request.ReadFromJsonAsync<RequestModel>();

    if (request is null)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid request.");
        return;
    }

    await handlerService.HandleRequestAsync(context, request);
});

app.MapPost("/img2img", async (HttpContext context, HandlerService handlerService) =>
{
    var request = await context.Request.ReadFromJsonAsync<RequestModel>();

    if (request is null)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid request.");
        return;
    }

    await handlerService.HandleRequestAsync(context, request);
});

app.MapPost("/api/chat", async (HttpContext context, HandlerService handlerService) =>
{
    var request = await context.Request.ReadFromJsonAsync<RequestModel>();

    if (request is null)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid request.");
        return;
    }

    request.KeepAlive = -1;
    request.Stream ??= true;

    await handlerService.HandleRequestAsync(context, request);
});

app.MapPost("/api/generate", async (HttpContext context, HandlerService handlerService) =>
{
    var request = await context.Request.ReadFromJsonAsync<RequestModel>();

    if (request is null)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid request.");
        return;
    }

    request.KeepAlive = -1;
    request.Stream ??= true;

    await handlerService.HandleRequestAsync(context, request);
});

app.MapPost("/api/embeddings", async (HttpContext context, HandlerService handlerService) =>
{
    var request = await context.Request.ReadFromJsonAsync<RequestModel>();

    if (request is null)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Invalid request.");
        return;
    }

    request.KeepAlive = -1;
    request.Stream ??= true;

    await handlerService.HandleRequestAsync(context, request);
});

app.Run();
