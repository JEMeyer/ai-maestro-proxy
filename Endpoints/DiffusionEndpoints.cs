using AIMaestroProxy.Handlers;

namespace AIMaestroProxy.Endpoints
{
    public static class DiffusionEndpoints
    {
        public static void MapDiffusionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/txt2img", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });

            endpoints.MapPost("/img2img", async (HttpContext context, ComputeHandler handlerService) =>
            {
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });
        }
    }
}
