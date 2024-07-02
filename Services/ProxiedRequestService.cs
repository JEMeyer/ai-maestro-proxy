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

            try
            {
                using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                await HandleResponseAsync(context, response);
            }
            catch (TaskCanceledException ex)
            {
                if (!context.Response.HasStarted)
                {
                    logger.LogError("Request was canceled: {ex}", ex);
                    context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                }
            }
            catch (Exception ex)
            {
                if (!context.Response.HasStarted)
                {
                    logger.LogError("Error routing request: {ex}", ex);
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }
            }
        }

        // TODO: This can take in the prop(s) that are supposed to get concated. can also pick some to not concat and just replace, etc
        public async Task RouteLoopingRequestAsync(HttpContext context, string path, IEnumerable<ContainerInfo> containerInfos)
        {
            logger.LogDebug("Handling looping request for {path}", path);
            var modelsList = new List<JsonElement>();

            foreach (var container in containerInfos)
            {
                var targetUri = new Uri($"http://{container.Ip}:{container.Port}/{path}{context.Request.QueryString}");
                var proxyRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);

                foreach (var header in context.Request.Headers)
                {
                    proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                using var response = await httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                // Read the full response content
                var responseBody = await response.Content.ReadAsStringAsync(context.RequestAborted);
                var models = JsonDocument.Parse(responseBody).RootElement.GetProperty("models").EnumerateArray();

                modelsList.AddRange(models);
            }

            // Create the result object
            var result = new { models = modelsList };

            // Set headers
            context.Response.Headers.Remove("Transfer-Encoding");
            context.Response.ContentLength = null;  // Remove any content length set previously

            // Write the response as JSON
            await context.Response.WriteAsJsonAsync(result);
        }


        private static async Task HandleResponseAsync(HttpContext context, HttpResponseMessage response)
        {
            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.Headers.Remove("Transfer-Encoding");

            if (response.Content.Headers.ContentLength.HasValue)
            {
                context.Response.ContentLength = response.Content.Headers.ContentLength.Value;
                var responseContent = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
                await context.Response.Body.WriteAsync(responseContent, context.RequestAborted);
            }
            else if (response.Headers.TransferEncodingChunked == true)
            {
                context.Response.Headers.Remove("Content-Length");
                await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
        }
    }
}
