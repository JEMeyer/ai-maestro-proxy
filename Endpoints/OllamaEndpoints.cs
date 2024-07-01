using AIMaestroProxy.Handlers;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Endpoints
{
    public static class OllamaEndpoints
    {
        public static void MapOllamaEndpoints(this IEndpointRouteBuilder endpoints)
        {
            // Naked URL and OpenAI url first, then all /api
            endpoints.MapGet("/", (HttpContext context) => "Ollama is running.");

            endpoints.MapPost("/v1/chat/completions", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("Ollama OpenAI endpoint hit");
            });

            // These should cover all the /api endpoints for Ollama
            endpoints.MapGet("/api/version", async (HttpContext context, OllamaHandler handlerService) =>
            {
                await handlerService.HandleOllamaProcessRequestAsync(context);
            });

            endpoints.MapPost("/api/show", async (HttpContext context, OllamaHandler handlerService) =>
            {
                var request = await RequestModelParser.ParseFromContext(context);
                await handlerService.HandleOllamaProcessRequestAsync(context, request.Name, request);
            });

            endpoints.MapPost("/api/chat", async (HttpContext context, OllamaHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapPost("/api/generate", async (HttpContext context, OllamaHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapPost("/api/embeddings", async (HttpContext context, OllamaHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapGet("/api/tags", async (HttpContext context, OllamaHandler ollamaHandler) =>
            {
                await ollamaHandler.HandleOllamaContainersRequestAsync(context, "tags");
            });

            endpoints.MapGet("/api/ps", async (HttpContext context, OllamaHandler ollamaHandler) =>
            {
                await ollamaHandler.HandleOllamaContainersRequestAsync(context, "ps");
            });

            // Stub endpoints
            endpoints.MapPost("/api/pull", async (HttpContext context) =>
            {
                // We can actually call something on the backend right?
                await context.Response.WriteAsync("PullModelHandler endpoint hit");
            });

            endpoints.MapPost("/api/create", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("CreateModelHandler endpoint hit");
            });

            endpoints.MapPost("/api/push", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("PushModelHandler endpoint hit");
            });

            endpoints.MapPost("/api/copy", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("CopyModelHandler endpoint hit");
            });

            endpoints.MapDelete("/api/delete", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("DeleteModelHandler endpoint hit");
            });

            endpoints.MapPost("/api/blobs/:digest", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("CreateBlobHandler endpoint hit");
            });

        }
    }
}
