using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class ComputeHandler(GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService, ILogger<ComputeHandler> logger)
    {
        // Handles requests
        public async Task HandleComputeRequestAsync(HttpContext context, string modelLookupKey, RequestModel request)
        {
            logger.LogDebug("Handling request which will need a gpu for {modelName}", modelLookupKey);
            // Try to get an available model assignment
            var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(modelLookupKey, context.RequestAborted);
            ArgumentNullException.ThrowIfNull(modelAssignment);

            var gpuIds = modelAssignment.GpuIds.Split(',');

            try
            {
                await proxiedRequestService.RouteRequestAsync(context, modelAssignment, request);
            }
            finally
            {
                gpuManagerService.UnlockGPUs(gpuIds);
            }
        }
    }
}
