using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIMaestroProxy.Models;
using Microsoft.Extensions.Primitives;

namespace AIMaestroProxy.Services
{
    public class ProxiedRequestService(HttpClient httpClient, ILogger<ProxiedRequestService> logger)
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async ValueTask RouteRequestAsync(HttpContext context, ModelAssignment modelAssignment, RequestModel? request = null)
        {
            logger.LogDebug("About to route a request with {assignmentName}", modelAssignment.Name);

            // We only get request if it was modified, otherwise we just read from context
            request ??= await RequestModelParser.ParseFromContext(context);

            logger.LogDebug("Starting to route a request to IP: {Ip}, Port: {Port}", modelAssignment.Ip, modelAssignment.Port);
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();
            var requestUri = $"http://{modelAssignment.Ip}:{modelAssignment.Port}{path}{queryString}";

            logger.LogDebug("The constructed request URI is: {Uri}", requestUri);

            var httpRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), requestUri);

            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();

                var multipartContent = new MultipartFormDataContent();

                foreach (var field in form)
                {
                    if (!StringValues.IsNullOrEmpty(field.Value))
                    {
                        foreach (var value in field.Value)
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                multipartContent.Add(new StringContent(value), field.Key);
                            }
                        }
                    }
                }

                foreach (var file in form.Files)
                {
                    var streamContent = new StreamContent(file.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    multipartContent.Add(streamContent, file.Name, file.FileName);
                }

                httpRequest.Content = multipartContent;
            }
            else
            {
                var content = JsonSerializer.Serialize(request, serializerOptions);
                httpRequest.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            logger.LogDebug("Sending with RequestModel: {model}", JsonSerializer.Serialize(request, serializerOptions));

            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted).ConfigureAwait(false);
            logger.LogDebug("Received a response with status code: {StatusCode}", response.StatusCode);

            response.EnsureSuccessStatusCode();

            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();

            if (request?.Stream == true)
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            else
            {
                var responseContent = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
                context.Response.ContentLength = responseContent.Length;
                await context.Response.Body.WriteAsync(responseContent, context.RequestAborted);
            }
        }
    }
}
