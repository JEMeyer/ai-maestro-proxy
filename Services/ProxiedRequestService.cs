using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class ProxiedRequestService(HttpClient httpClient, ILogger<ProxiedRequestService> logger)
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async ValueTask RouteRequestAsync(HttpContext context, RequestModel request, ModelAssignment modelAssignment)
        {
            logger.LogDebug("Starting to route a request to IP: {Ip}, Port: {Port}", modelAssignment.Ip, modelAssignment.Port);
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();
            var requestUri = $"http://{modelAssignment.Ip}:{modelAssignment.Port}{path}{queryString}";

            logger.LogDebug("The constructed request URI is: {Uri}", requestUri);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(request, serializerOptions), Encoding.UTF8, "application/json")
            };

            // Send the request and get the response
            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted).ConfigureAwait(false);
            logger.LogDebug("Received a response with status code: {StatusCode}", response.StatusCode);

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
        }
    }
}
