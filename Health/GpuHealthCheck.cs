using AIMaestroProxy.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIMaestroProxy.Health
{
    public class GpuHealthCheck(IGpuManagerService gpuManager) : IHealthCheck
    {
        private readonly IGpuManagerService _gpuManager = gpuManager;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
            => await Task.Run(CheckHealth);

        private HealthCheckResult CheckHealth()
        {
            try
            {
                if (_gpuManager.IsHealthy())
                {
                    return HealthCheckResult.Healthy("GPU service is healthy");
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
