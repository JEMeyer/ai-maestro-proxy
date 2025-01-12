using AIMaestroProxy.Middleware;
using AIMaestroProxy.Services;

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
            services.AddSingleton<CacheService>();
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<DataService>();
            services.AddSingleton<GpuManagerService>();
            services.AddControllers();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole();
            });

            return services;
        }
    }
}
