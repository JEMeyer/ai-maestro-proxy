namespace ai_maestro_proxy.Middleware
{
    public class TraceIdLoggingMiddleware(RequestDelegate next, ILogger<TraceIdLoggingMiddleware> _logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var traceIdentifier = context.TraceIdentifier;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceIdentifier"] = traceIdentifier
            }))
            {
                await next(context);
            }
        }
    }
}
