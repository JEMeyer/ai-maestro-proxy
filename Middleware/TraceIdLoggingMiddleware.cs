namespace AIMaestroProxy.Middleware
{
    public class TraceIdLoggingMiddleware(RequestDelegate next, ILogger<TraceIdLoggingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceIdentifier"] = context.TraceIdentifier,
                ["RequestPath"] = context.Request.Path.Value ?? "/"
            }))
            {
                await next(context);
            }
        }
    }
}
