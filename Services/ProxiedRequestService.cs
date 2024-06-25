using System.Text;
using System.Text.Json;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.Services
{
    public class ProxiedRequestService(HttpClient httpClient)
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public async Task RouteRequestAsync(HttpContext context, RequestModel request, Assignment assignment, CancellationToken cancellationToken)
        {
            var requestUri = $"http://{assignment.Ip}:{assignment.Port}/api";

            // Serialize the request model, including additional data
            var requestBody = JsonSerializer.Serialize(request, serializerOptions);

            var requestContent = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = requestContent
            };

            // Send the request and get the response
            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode(); // Throw if not a success code.

            // Check if the request has a 'stream' property
            if (request.Stream.GetValueOrDefault())
            {
                // Stream the response back to the client
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await responseStream.CopyToAsync(context.Response.Body, cancellationToken);
            }
            else
            {
                // Read the response content as a string and send it back
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                await context.Response.WriteAsync(responseContent, cancellationToken);
            }
        }
    }
}
