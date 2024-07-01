using System.Text.Json;
using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class OllamaHandler(DataService dataService, ComputeHandler computeHandler, ProxiedRequestService proxiedRequestService, ILogger<OllamaHandler> logger)
    {

        public async Task HandleOllamaComputeRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);
            ArgumentNullException.ThrowIfNull(request.Model);
            request.KeepAlive = -1;
            request.Stream ??= true;

            await computeHandler.HandleComputeRequestAsync(context, request.Model, request);
        }

        // Just call any server that has our model, we shouldn't need GPUs for this so not reserving
        public async Task HandleOllamaProcessRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);
            ArgumentNullException.ThrowIfNull(request.Name);

            // Try to get an available model assignment
            var modelAssignment = (await dataService.GetModelAssignmentsAsync(request.Name)).First();
            ArgumentNullException.ThrowIfNull(modelAssignment);

            await proxiedRequestService.RouteRequestAsync(context, modelAssignment, request);
        }

        // Handles endpoints that would require calling every container (ps or tags) to give an accurate reading
        public async Task HandleOllamaContainersRequestAsync(HttpContext context, string path)
        {
            var containerInfos = await dataService.GetLlmContainerInfosAsync();
            var modelsJsonStrings = new List<string>();
            using HttpClient client = new();

            foreach (var containerInfo in containerInfos)
            {
                var response = await client.GetStringAsync($"http://{containerInfo.Ip}:{containerInfo.Port}/api/{path}");
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
