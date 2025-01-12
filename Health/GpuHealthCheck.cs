using AIMaestroProxy.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIMaestroProxy.Health
{
    public class GpuHealthCheck(IGpuManagerService gpuManager) : IHealthCheck
    {
        private readonly IGpuManagerService _gpuManager = gpuManager;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _gpuManager.IsHealthyAsync())
                {
                    var status = _gpuManager.GetCurrentGpuStatus();
                    return HealthCheckResult.Healthy("GPU service is healthy",
                        new Dictionary<string, object>
                        {
                        { "gpu_statuses", status.ToDictionary(s => s.GpuId, s => s.IsLocked) }
                        });
                }
                return HealthCheckResult.Unhealthy("GPU service check failed");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(ex.Message);
            }
        }
    }
}
