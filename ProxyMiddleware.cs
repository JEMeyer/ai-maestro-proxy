using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;

public class ProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory)
{
    private readonly RequestDelegate _next = next;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();

    public async Task InvokeAsync(HttpContext context)
    {
        var targetUri = new Uri("http://static-ip:port" + context.Request.Path + context.Request.QueryString);

        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = targetUri
        };

        // Copy the request headers
        foreach (var header in context.Request.Headers)
        {
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        if (context.Request.ContentLength > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        var responseMessage = await _httpClient.SendAsync(requestMessage);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");

        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
}
