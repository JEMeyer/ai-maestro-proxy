namespace AIMaestroProxy.Middleware
{
    public class TraceIdLoggingMiddleware(RequestDelegate next, ILogger<TraceIdLoggingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var traceIdentifier = context.TraceIdentifier;

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceIdentifier"] = traceIdentifier
            }))
            {
                await next(context);
            }
        }
    }
}
