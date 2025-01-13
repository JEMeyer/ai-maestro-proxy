using System.Diagnostics;

namespace AIMaestroProxy.Middleware
{
    public class StopwatchMiddleware(RequestDelegate next, ILogger<StopwatchMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            // Create a new logging scope with the current request's path
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestPath"] = context.Request.Path.Value ?? "/"
            }))
            {
                await next(context);

                stopwatch.Stop();
                logger.LogDebug("##COLOR## Total request time: {milliseconds} ms.", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
