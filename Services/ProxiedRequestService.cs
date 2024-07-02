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

    public class ProxiedRequestService2(HttpClient httpClient, ILogger<ProxiedRequestService> logger)
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
        public async Task HandleLoopingRequestAsync(HttpContext context, string path, IEnumerable<ContainerInfo> containerInfos)
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
