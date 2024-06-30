using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class ComputeHandler(GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService, ILogger<ComputeHandler> logger)
    {
        private async Task HandleComputeRequestAsync(HttpContext context, RequestModel request)
        {
            logger.LogDebug("Handling request for model: {Model}", request.ModelName);
            // Try to get an available model modelAssignment
            ArgumentNullException.ThrowIfNull(request.ModelName);
            var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(request.ModelName, context.RequestAborted);
            ArgumentNullException.ThrowIfNull(modelAssignment);

            var gpuIds = modelAssignment.GpuIds.Split(',');

            try
            {
                await proxiedRequestService.RouteRequestAsync(context, request, modelAssignment);
            }
            finally
            {
                gpuManagerService.UnlockGPUs(gpuIds);
            }
        }

        public async Task HandleOllamaComputeRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);

            request.KeepAlive = -1;
            request.Stream ??= true;

            await HandleComputeRequestAsync(context, request);
        }

        public async Task HandleDiffusionComputeRequestAsync(HttpContext context)
        {
            await HandleComputeRequestAsync(context, await RequestModelParser.ParseFromContext(context));
        }
    }
}
