using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.Services
{
    public class ProxiedRequestService(HttpClient httpClient, ILogger<ProxiedRequestService> _logger)
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async ValueTask RouteRequestAsync(HttpContext context, RequestModel request, Assignment assignment)
        {
            _logger.LogInformation("Starting to route a request to IP: {Ip}, Port: {Port}", assignment.Ip, assignment.Port);
            var stopWatch = Stopwatch.StartNew();
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();
            var requestUri = $"http://{assignment.Ip}:{assignment.Port}{path}{queryString}";

            _logger.LogDebug("The constructed request URI is: {Uri}", requestUri);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(request, serializerOptions), Encoding.UTF8, "application/json")
            };

            try
            {
                _logger.LogInformation("##COLOR##The request has taken  {ElapsedMicroseconds} μs to proxy.", stopWatch.Elapsed.Microseconds);
                // Send the request and get the response
                using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted).ConfigureAwait(false);
                _logger.LogDebug("Received a response with status code: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                if (request.Stream.GetValueOrDefault())
                {
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
                    await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                    await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync(context.RequestAborted);
                    await context.Response.WriteAsync(responseContent, context.RequestAborted);
                }

                _logger.LogInformation("##COLOR##Response successfully proxied to client. Total request time {ElapsedMilliseconds} ms", stopWatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("##COLOR##Request was cancelled after {ElapsedMilliseconds} ms", stopWatch.ElapsedMilliseconds);
                throw; // Re-throw the exception to propagate the cancellation
            }
        }
    }
}
