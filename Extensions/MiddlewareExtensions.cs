using AIMaestroProxy.Interfaces;
using AIMaestroProxy.Logging;
using AIMaestroProxy.Middleware;
using AIMaestroProxy.Services;
using Microsoft.Extensions.Logging.Console;
using MySql.Data.MySqlClient;
using StackExchange.Redis;

namespace AIMaestroProxy.Extensions
{
    public static class MiddlewareExtensions
    {
        public static void UseLogging(this IApplicationBuilder app)
        {
            app.UseMiddleware<TraceIdLoggingMiddleware>();
            app.UseMiddleware<StopwatchMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            app.UseMiddleware<NotFoundLoggingMiddleware>();
        }

        public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<ProxiedRequestService>();

            var redisConnectionString = configuration.GetConnectionString("Redis");
            ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);

            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<MySqlConnection>(_ => new(configuration.GetConnectionString("MariaDb")));

            services.AddSingleton<CacheService>();
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<DataService>();
            services.AddSingleton<IGpuManagerService, GpuManagerService>();
            services.AddControllers();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(options => options.FormatterName = "custom");
                loggingBuilder.AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>();
            });

            return services;
        }
    }
}
