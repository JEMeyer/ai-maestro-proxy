namespace ai_maestro_proxy.Middleware
{

    public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = Guid.NewGuid().ToString();
            context.Items["CorrelationId"] = correlationId;

            // Set the correlation ID in the response headers for client access
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Append("X-Correlation-ID", correlationId);
                return Task.CompletedTask;
            });

            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                // Log the correlation ID and request path at the start of the request
                var requestPath = context.Request.Path.ToString();
                var requestId = context.TraceIdentifier;

                logger.LogInformation("Starting request with Correlation ID: {CorrelationId}, Request Path: {RequestPath}, Request ID: {RequestId}", correlationId, requestPath, requestId);

                await next(context);
            }
        }
    }
}
