using System.Diagnostics;
using System.Text.Json;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class OllamaHandler(DataService dataService, ILogger<OllamaHandler> logger, Stopwatch stopwatch)
    {
        public async Task HandleTagsRequestAsync(HttpContext context)
        {
            var containerInfos = await dataService.GetLlmContainerInfosAsync();
            var models = new List<JsonElement>();
            using HttpClient client = new();

            foreach (var containerInfo in containerInfos)
            {
                var response = await client.GetStringAsync($"http://{containerInfo.Ip}:{containerInfo.Port}/api/tags");
                using JsonDocument doc = JsonDocument.Parse(response);

                var responseModels = doc.RootElement.GetProperty("models").EnumerateArray();
                models.AddRange(responseModels);
                logger.LogDebug("Added {responseModelsCount} containerInfos to response.", responseModels.Count());
            }

            // Create a JSON object containing the list of models
            var jsonObject = new { models };

            // Convert the JSON object to a string
            var jsonString = JsonSerializer.Serialize(jsonObject);

            // Set the ContentType property of the HttpResponse object to indicate that the response is in JSON format
            context.Response.ContentType = "application/json";

            // Write the JSON data to the response stream using the WriteAsync() method
            logger.LogDebug("##COLOR##/api/tags successfully proxied to client(s). Total request time {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            await context.Response.WriteAsync(jsonString);
        }
    }
}
