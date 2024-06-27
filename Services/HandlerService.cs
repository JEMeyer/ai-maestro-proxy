using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class HandlerService(GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService, ILogger<HandlerService> _logger)
    {
        public static async Task<RequestModel> ParseRequestModelFromContext(HttpContext context)
        {
            var request = await context.Request.ReadFromJsonAsync<RequestModel>() ?? throw new ArgumentException("Invalid request.");
            return request;
        }

        public async Task HandleRequestAsync(HttpContext context, RequestModel request)
        {
            _logger.LogDebug("Handling request for model: {Model}", request.Model);
            // Try to get an available assignment
            var assignment = await gpuManagerService.GetAvailableAssignmentAsync(request.Model, context.RequestAborted);
            ArgumentNullException.ThrowIfNull(assignment);

            var gpuIds = assignment.GpuIds.Split(',');

            try
            {
                await proxiedRequestService.RouteRequestAsync(context, request, assignment);
            }
            finally
            {
                gpuManagerService.UnlockGPUs(gpuIds);
            }
        }

        public async Task HandleOllamaRequestAsync(HttpContext context)
        {
            var request = await ParseRequestModelFromContext(context);

            request.KeepAlive = -1;
            request.Stream ??= true;

            await HandleRequestAsync(context, request);
        }

        public async Task HandleDiffusionRequestAsync(HttpContext context)
        {
            await HandleRequestAsync(context, await ParseRequestModelFromContext(context));
        }
    }
}
