using AIMaestroProxy.Handlers;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Endpoints
{
    public static class DiffusionEndpoints
    {
        public static void MapDiffusionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/txt2img", async (HttpContext context, DiffusionHandler handlerService) =>
            {
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });

            endpoints.MapPost("/img2img", async (HttpContext context, DiffusionHandler handlerService) =>
            {
                var request = await RequestModelParser.ParseFromContext(context);
                ArgumentNullException.ThrowIfNull(request.Model);
                await handlerService.HandleDiffusionComputeRequestAsync(context);
            });

            endpoints.MapPost("/upload", async (HttpContext context, DiffusionHandler handlerService) =>
            {
                var request = await RequestModelParser.ParseFromContext(context);
                ArgumentNullException.ThrowIfNull(request.Model);
                await handlerService.HandleDiffusionProcessRequestAsync(context);
            });
        }
    }
}
