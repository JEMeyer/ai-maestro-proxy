using AIMaestroProxy.Handlers;

namespace AIMaestroProxy.Endpoints
{
    public static class IFWhisperEndpoints
    {
        public static void MapIFWhisperEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/transcribe", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });

            endpoints.MapPost("/transcribe/stream", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });
        }
    }
}
