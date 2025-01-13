using AIMaestroProxy.Interfaces;

namespace AIMaestroProxy.Services
{
    public class GpuReleaseService(DataService dataService, IGpuManagerService gpuManagerService, ILogger<GpuReleaseService> logger) : BackgroundService
    {
        private readonly DataService _dataService = dataService;
        private readonly IGpuManagerService _gpuManagerService = gpuManagerService;
        private readonly ILogger<GpuReleaseService> _logger = logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30); // Check every 30 seconds
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1); // GPU inactivity timeout

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ReleaseStuckGPUs();
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private void ReleaseStuckGPUs()
        {
            lock (_gpuManagerService.GpuLockObject)
            {
                var currentTime = DateTime.UtcNow;
                var allGpuStatuses = _dataService.GetAllGpuStatuses();
                List<string> gpuIdsToUnlock = [];
                foreach (var status in allGpuStatuses)
                {
                    if (status.LastActivity.HasValue &&
                        currentTime - status.LastActivity.Value > _timeout &&
                        status.IsLocked)
                    {
                        gpuIdsToUnlock.Add(status.GpuId);
                    }
                }

                if (gpuIdsToUnlock.Count > 0)
                {
                    _logger.LogWarning("GPU(S) STUCK: Releasing: {gpus}", gpuIdsToUnlock.ToString());
                    _gpuManagerService.UnlockGPUs([.. gpuIdsToUnlock]);
                }
            }
        }
    }
}
