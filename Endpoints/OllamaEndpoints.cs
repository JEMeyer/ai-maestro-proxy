using AIMaestroProxy.Handlers;

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
                await context.Response.WriteAsync("ProcessHandler endpoint hit");
            });

            // These should cover all the /api endpoints for Ollama
            endpoints.MapGet("/api/version", (HttpContext context, IConfiguration configuration) =>
            {
                var version = configuration["Maestro:OllamaVersion"];
                return Results.Ok(new { version });
            });

            endpoints.MapPost("/api/show", async (HttpContext context, OllamaHandler handlerService) =>
            {
                await handlerService.HandleModelRequestAsync(context);
            });

            endpoints.MapPost("/api/chat", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapPost("/api/generate", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapPost("/api/embeddings", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleOllamaComputeRequestAsync(context);
            });

            endpoints.MapGet("/api/tags", async (HttpContext context, OllamaHandler ollamaHandler) =>
            {
                await ollamaHandler.HandleContainersRequestAsync(context, "tags");
            });

            // Stub endpoints
            endpoints.MapPost("/api/pull", async (HttpContext context) =>
            {
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

            endpoints.MapGet("/api/ps", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("ProcessHandler endpoint hit");
            });
        }
    }
}
