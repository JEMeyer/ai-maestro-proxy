using System.Diagnostics;
using System.Text.Json;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class OllamaHandler(DataService dataService, ILogger<OllamaHandler> logger)
    {
        public async Task HandleTagsRequestAsync(HttpContext context)
        {
            var containerInfos = await dataService.GetLlmContainerInfosAsync();
            var modelsJsonStrings = new List<string>();
            using HttpClient client = new();

            foreach (var containerInfo in containerInfos)
            {
                var response = await client.GetStringAsync($"http://{containerInfo.Ip}:{containerInfo.Port}/api/tags");
                using JsonDocument doc = JsonDocument.Parse(response);

                var responseModels = doc.RootElement.GetProperty("models").EnumerateArray();

                foreach (var responseModel in responseModels)
                {
                    modelsJsonStrings.Add(responseModel.GetRawText());
                }

                logger.LogDebug("Added {responseModelsCount} containerInfos to response.", responseModels.Count());
            }

            // Create the final JSON object with the models array
            var concatenatedModels = "{\"models\":[" + string.Join(",", modelsJsonStrings) + "]}";

            // Set the ContentType property of the HttpResponse object to indicate that the response is in JSON format
            context.Response.ContentType = "application/json";

            // Write the JSON data to the response stream using the WriteAsync() method
            await context.Response.WriteAsync(concatenatedModels);
        }
    }
}
