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
            if (assignment == null)
            {
                context.Response.StatusCode = 503; // Service Unavailable
                await context.Response.WriteAsync("No assignments available. Please try again later.");
                _logger.LogError("No assignments available for {Model}. Please try again later.", request.Model);
                return;
            }

            var gpuIds = assignment.GpuIds.Split(',');

            if (gpuManagerService.TryLockGPUs(gpuIds))
            {
                try
                {
                    var cancellationToken = context.RequestAborted;
                    await proxiedRequestService.RouteRequestAsync(context, request, assignment, cancellationToken);
                }
                finally
                {
                    gpuManagerService.UnlockGPUs(gpuIds);
                }
            }
            else
            {
                context.Response.StatusCode = 503; // Service Unavailable
                await context.Response.WriteAsync("GPUs are currently in use. Please try again later.");
            }
        }
    }
}
