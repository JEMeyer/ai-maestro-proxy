using AIMaestroProxy.Extensions;
using AIMaestroProxy.Health;
using AIMaestroProxy.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.WebHost.UseSentry();

// Register services
builder.Services.AddServices(builder.Configuration);
builder.Services.AddHostedService<GpuReleaseService>(); // runs in the background to release gpus w/o activity
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<GpuHealthCheck>("gpu_health");

// Build the application
var app = builder.Build();
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

// Use logging and other middlewares
app.UseLogging();
app.UseHttpMetrics(options =>
{
    // This will preserve only the first digit of the status code.
    // For example: 200, 201, 203 -> 2xx
    options.ReduceStatusCodeCardinality();
});
app.MapHealthChecks("/health");
app.MapControllers();
app.MapMetrics();
app.Run();
