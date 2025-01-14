using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class ProxiedRequestService(
        HttpClient httpClient,
        ILogger<ProxiedRequestService> logger
    )
    {
        /// <summary>
        /// Routes the request to the target container and streams the response back to the client.
        /// </summary>
        public async Task RouteRequestAsync(
            HttpContext context,
            ModelAssignment modelAssignment,
            HttpMethod method,
            string? body
        )
        {
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();
            var requestUri = $"http://{modelAssignment.Ip}:{modelAssignment.Port}{path}{queryString}";

            using var httpRequest = new HttpRequestMessage(method, requestUri);

            // Attach body if necessary
            if (method != HttpMethod.Get && method != HttpMethod.Head && !string.IsNullOrEmpty(body))
            {
                httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(context.Request.ContentType ?? "application/json");
            }

            // Copy request headers, excluding hop-by-hop headers
            foreach (var header in context.Request.Headers)
            {
                if (!ShouldSkipHeader(header.Key))
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            try
            {
                // Send the request with streaming response
                using var response = await httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted
                );

                // Set the response status code
                context.Response.StatusCode = (int)response.StatusCode;

                // Copy response headers, excluding hop-by-hop headers
                foreach (var header in response.Headers)
                {
                    if (!ShouldSkipHeader(header.Key))
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }
                foreach (var header in response.Content.Headers)
                {
                    if (!ShouldSkipHeader(header.Key))
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }

                // Remove Transfer-Encoding if present to avoid issues
                context.Response.Headers.Remove("Transfer-Encoding");

                // Stream the response content directly to the client
                await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (TaskCanceledException ex)
            {
                // Handle client disconnects gracefully
                if (!context.Response.HasStarted)
                {
                    logger.LogError(ex, "Request was canceled by the client.");
                    context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                }
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                if (!context.Response.HasStarted)
                {
                    logger.LogError(ex, "Error routing request.");
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }
            }
        }

        /// <summary>
        /// Determines if a header should be skipped when copying.
        /// </summary>
        private static bool ShouldSkipHeader(string headerKey)
        {
            // List of hop-by-hop headers to skip
            var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Connection",
                "Keep-Alive",
                "Proxy-Authenticate",
                "Proxy-Authorization",
                "TE",
                "Trailer",
                "Transfer-Encoding",
                "Upgrade",
                "Host"
            };

            return hopByHopHeaders.Contains(headerKey);
        }

        public async Task<string?> ModifyRequestBodyAsync(Stream requestBody)
        {
            using var reader = new StreamReader(requestBody);
            var bodyJson = await reader.ReadToEndAsync();
            logger.LogInformation($"body json was '{bodyJson}'");
            if (string.IsNullOrWhiteSpace(bodyJson)) return null;

            var bodyObj = JsonSerializer.Deserialize<Dictionary<string, object>>(bodyJson);
            if (bodyObj == null)
            {
                logger.LogError("Failed to deserialize JSON: {BodyJson}", bodyJson);
                return null;
            }

            bodyObj["keepAlive"] = -1;
            return JsonSerializer.Serialize(bodyObj);
        }
    }
}
