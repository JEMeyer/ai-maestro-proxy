namespace ai_maestro_proxy.Utilities
{
    public static class HttpContextExtensions
    {
        public static async Task<string> ReadRequestBodyAsync(this HttpContext context)
        {
            context.Request.EnableBuffering();
            using StreamReader reader = new(context.Request.Body, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            return body;
        }

        public static void WriteRequestBody(this HttpContext context, string body)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
        }
    }
}
