using System.Net;
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
        public async Task RouteLoopingRequestAsync(HttpContext context, string path, IEnumerable<ContainerInfo> containerInfos, bool forceChunked = false)
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
                var responseBody = await response.Content.ReadAsStringAsync(context.RequestAborted);
                var models = JsonDocument.Parse(responseBody).RootElement.GetProperty("models").EnumerateArray();

                modelsList.AddRange(models);
            }

            var result = new { models = modelsList };
            var resultJson = JsonSerializer.Serialize(result);
            var resultContent = new StringContent(resultJson, Encoding.UTF8, "application/json");

            var finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = resultContent
            };

            // Use HandleResponseAsync to handle the final response, with the forceChunked flag
            await HandleResponseAsync(context, finalResponse, forceChunked);
        }

        private static async Task HandleResponseAsync(HttpContext context, HttpResponseMessage response, bool isChunked = false)
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

            if (isChunked)
            {
                context.Response.Headers.TransferEncoding = "chunked";

                var chunkSize = 8192;
                var buffer = new byte[chunkSize];
                await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, context.RequestAborted)) > 0)
                {
                    var chunkHeader = $"{bytesRead:X}\r\n";
                    await context.Response.Body.WriteAsync(Encoding.ASCII.GetBytes(chunkHeader), context.RequestAborted);
                    await context.Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), context.RequestAborted);
                    await context.Response.Body.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), context.RequestAborted);
                }

                await context.Response.Body.WriteAsync(Encoding.ASCII.GetBytes("0\r\n\r\n"), context.RequestAborted); // End of chunks
            }
            else
            {
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    context.Response.ContentLength = response.Content.Headers.ContentLength.Value;
                    var responseContent = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
                    await context.Response.Body.WriteAsync(responseContent, context.RequestAborted);
                }
                else
                {
                    context.Response.ContentLength = null;
                    await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                    await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
                }
            }
        }
    }
}
