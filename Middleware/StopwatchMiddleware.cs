using System.Diagnostics;

namespace AIMaestroProxy.Middleware
{
    public class StopwatchMiddleware(RequestDelegate next, ILogger<StopwatchMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            await next(context);

            stopwatch.Stop();

            logger.LogDebug("##COLOR##Total request time: {milliseconds} ms.", stopwatch.ElapsedMilliseconds);
        }
    }
}
