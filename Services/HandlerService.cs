using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.Services
{
    public class HandlerService(GpuManagerService gpuManagerService, ProxiedRequestService proxiedRequestService, ILogger<HandlerService> _logger)
    {
        public async Task HandleRequestAsync(HttpContext context, RequestModel request)
        {
            _logger.LogInformation("Handling request for model: {Model}", request.Model);
            // Try to get an available assignment
            var assignment = await gpuManagerService.GetAvailableAssignmentAsync(request.Model);

            var gpuIds = assignment.GpuIds.Split(',');

            try
            {
                var cancellationToken = context.RequestAborted;
                await proxiedRequestService.RouteRequestAsync(context, request, assignment, cancellationToken);
            }
            finally
            {
                gpuManagerService.UnlockGPUs(gpuIds);
                await gpuManagerService.ProcessQueues();
            }
        }
    }
}
