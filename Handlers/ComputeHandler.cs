using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class ComputeHandler(GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService, ILogger<ComputeHandler> logger)
    {
        internal static async Task<RequestModel> ParseRequestModelFromContext(HttpContext context)
        {
            var request = await context.Request.ReadFromJsonAsync<RequestModel>() ?? throw new ArgumentException("Invalid request.");
            return request;
        }

        private async Task HandleComputeRequestAsync(HttpContext context, RequestModel request)
        {
            logger.LogDebug("Handling request for model: {Model}", request.Model);
            // Try to get an available model modelAssignment
            ArgumentNullException.ThrowIfNull(request.Model);
            var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(request.Model, context.RequestAborted);
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
            var request = await ParseRequestModelFromContext(context);

            request.KeepAlive = -1;
            request.Stream ??= true;

            await HandleComputeRequestAsync(context, request);
        }

        public async Task HandleDiffusionComputeRequestAsync(HttpContext context)
        {
            await HandleComputeRequestAsync(context, await ParseRequestModelFromContext(context));
        }
    }
}
