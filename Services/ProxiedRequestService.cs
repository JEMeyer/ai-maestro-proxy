using System.Text;
using System.Text.Json;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class ProxiedRequestService(HttpClient httpClient, ILogger<ProxiedRequestService> logger)
    {
        public async Task RouteRequestAsync(HttpContext context, ModelAssignment modelAssignment, HttpMethod method, string? body)
        {
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();
            var requestUri = $"http://{modelAssignment.Ip}:{modelAssignment.Port}{path}{queryString}";

            var httpRequest = new HttpRequestMessage(method, requestUri);

            if (method != HttpMethod.Get && method != HttpMethod.Head && !string.IsNullOrEmpty(body))
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                httpRequest.Content = content;
            }

            foreach (var header in context.Request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            if (response.Content.Headers.ContentType?.MediaType == "application/octet-stream" || response.Headers.TransferEncodingChunked == true)
            {
                context.Response.Headers.Remove("Content-Length");
                await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            else
            {
                var responseContent = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
                context.Response.Headers.Remove("Transfer-Encoding");
                context.Response.ContentLength = responseContent.Length;
                await context.Response.Body.WriteAsync(responseContent, context.RequestAborted);
            }
        }

        // TODO: This can take in the prop(s) that are supposed to get concated. can also pick some to not concat and just replace, etc
        public async Task RouteLoopingRequestAsync(HttpContext context, string path, IEnumerable<ContainerInfo> containerInfos)
        {
            logger.LogDebug("Handling looping request for {path}", path);
            var modelsList = new List<string>();

            foreach (var container in containerInfos)
            {
                var targetUri = new Uri($"http://{container.Ip}:{container.Port}/{path}{context.Request.QueryString}");
                var proxyRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);

                foreach (var header in context.Request.Headers)
                {
                    proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                var response = await httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                var responseBody = await response.Content.ReadAsStringAsync();
                var models = JsonDocument.Parse(responseBody).RootElement.GetProperty("models").EnumerateArray();
                modelsList.AddRange(models.Select(model => model.GetString() ?? string.Empty));
            }

            var result = new { models = modelsList };
            await context.Response.WriteAsJsonAsync(result);
        }
    }
}
