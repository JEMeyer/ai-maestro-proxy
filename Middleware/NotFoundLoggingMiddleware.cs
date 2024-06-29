namespace AIMaestroProxy.Middleware
{
    public class NotFoundLoggingMiddleware(RequestDelegate next, ILogger<NotFoundLoggingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            await next(context);

            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                logger.LogError("404 Not Found: {Path}", context.Request.Path);
            }
        }
    }
}
