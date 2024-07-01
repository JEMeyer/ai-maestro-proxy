namespace AIMaestroProxy.Middleware
{
    public class ConditionalContentLengthMiddleware(RequestDelegate next)
    {

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await next(context);

            if (!context.Items.ContainsKey("SkipContentLength"))
            {
                context.Response.Headers["Content-Length"] = responseBody.Length.ToString();
            }

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}
