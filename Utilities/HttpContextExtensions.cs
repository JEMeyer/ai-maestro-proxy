using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ai_maestro_proxy.Utilities
{
    public static class HttpContextExtensions
    {
        public static async Task<string> ReadRequestBodyAsync(this HttpContext context)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            return body;
        }

        public static void WriteRequestBody(this HttpContext context, string body)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
        }
    }
}
