namespace ai_maestro_proxy.Middleware
{
    public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var traceIdentifier = context.TraceIdentifier;
            var requestPath = context.Request.Path.ToString();

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceIdentifier"] = traceIdentifier,
                ["RequestPath"] = requestPath
            }))
            {
                logger.LogInformation("{TraceIdentifier} {RequestPath}", traceIdentifier, requestPath);
                await next(context);
            }
        }
    }
}
