using AIMaestroProxy.Controllers;
using AIMaestroProxy.Extensions;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Register services
builder.Services.AddServices(builder.Configuration);

// Build the application
var app = builder.Build();

// Use logging and other middlewares
app.UseLogging();
app.UseWebSockets();

// Map routes
app.MapControllerRoute(
    name: "default",
    pattern: "{*path}",
    defaults: new { controller = "Proxy", action = "HandleRequest" });

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var webSocketHandler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await webSocketHandler.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Run the application
app.Run();
