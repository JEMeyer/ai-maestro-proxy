using System.Text;
using Serilog;

namespace ai_maestro_proxy.Services
{
    public class ProxiedRequestService(IHttpClientFactory httpClientFactory)
    {
        public HttpRequestMessage CreateProxiedRequestMessage(string updatedRequestBody, Uri proxyUri, HttpContext httpContext)
        {
            var requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(httpContext.Request.Method),
                RequestUri = proxyUri,
                Content = new StringContent(updatedRequestBody, Encoding.UTF8, "application/json")
            };

            foreach (var header in httpContext.Request.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]);
            }

            return requestMessage;
        }

        public async Task SendProxiedRequest(HttpRequestMessage requestMessage, HttpContext httpContext, CancellationToken cancellationToken)
        {
            HttpClient client = httpClientFactory.CreateClient();

            HttpResponseMessage? responseMessage = null;
            try
            {
                Log.Information("Sending proxied request to {ProxyUri}", requestMessage.RequestUri);
                responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                httpContext.Response.StatusCode = (int)responseMessage.StatusCode;
                foreach (var header in responseMessage.Headers)
                {
                    httpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                foreach (var header in responseMessage.Content.Headers)
                {
                    httpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }

                httpContext.Response.Headers.Remove("transfer-encoding");
                await responseMessage.Content.CopyToAsync(httpContext.Response.Body, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.InnerException is TaskCanceledException)
            {
                Log.Information("Request to {ProxyUri} was canceled.", requestMessage.RequestUri);
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            catch (HttpRequestException ex)
            {
                var statusCode = responseMessage?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError;
                var responseBody = responseMessage != null ? await responseMessage.Content.ReadAsStringAsync(cancellationToken) : "No response body";
                Log.Error(ex, "HTTP request to {ProxyUri} failed with status code {StatusCode}. Response body: {ResponseBody}", requestMessage.RequestUri, statusCode, responseBody);
                httpContext.Response.StatusCode = (int)statusCode;
                await httpContext.Response.WriteAsync(responseBody, cancellationToken: cancellationToken);
            }
        }
    }
}
