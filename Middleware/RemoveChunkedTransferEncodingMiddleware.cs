namespace AIMaestroProxy.Middleware
{
    public class RemoveChunkedTransferEncodingMiddleware(RequestDelegate next, ILogger<RemoveChunkedTransferEncodingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            // Call the next middleware in the pipeline
            await next(context);

            // Check if the Transfer-Encoding header is present and set to chunked
            if (context.Response.Headers.TryGetValue("Transfer-Encoding", out Microsoft.Extensions.Primitives.StringValues value) &&
value == "chunked")
            {
                logger.LogDebug("Removing Transfer-Encoding: chunked header from the response.");
                // Remove the Transfer-Encoding header
                context.Response.Headers.Remove("Transfer-Encoding");
            }
        }
    }
}
