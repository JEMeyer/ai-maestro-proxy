using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;
using static AIMaestroProxy.Models.PathCategories;
using AIMaestroProxy.Interfaces;

namespace AIMaestroProxy.Services
{
    public partial class GpuManagerService(DataService dataService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger) : IGpuManagerService
    {
        private readonly DataService dataService = dataService;
        private readonly ILogger<GpuManagerService> logger = logger;
        private readonly ISubscriber subscriber = redis.GetSubscriber();
        public object GpuLockObject { get; } = new();
        private readonly ManualResetEventSlim gpusFreedEvent = new(false);
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");

        private bool TryLockGPUs(string[] gpuIds, string modelName)
        {
            try
            {
                logger.LogTrace("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var allUnlocked = gpuIds.All(gpuId =>
                {
                    var gpuStatus = dataService.GetGpuStatus(gpuId);
                    return !gpuStatus?.IsLocked ?? true;
                });

                if (allUnlocked)
                {
                    logger.LogDebug("Locking : {GpuIds}", string.Join(", ", gpuIds));
                    var currentTime = DateTime.UtcNow;

                    foreach (var gpuId in gpuIds)
                    {
                        var newStatus = new GpuStatus
                        {
                            GpuId = gpuId,
                            CurrentModel = modelName,
                            LastActivity = currentTime
                        };
                        dataService.SetGpuStatus(gpuId, newStatus);
                    }

                    // Notify other instances about the change
                    var lockedGpus = gpuIds.ToDictionary(
                        gpuId => gpuId,
                        _ => modelName
                    );
                    subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(lockedGpus));
                    return true;
                }

                logger.LogWarning("Failed to lock : {GpuIds}", string.Join(", ", gpuIds));
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in LockGPUs for GPUs {GpuIds}", string.Join(", ", gpuIds));
                throw;
            }
        }

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (GpuLockObject)
            {
                logger.LogDebug("Unlocking GPUs : {GpuIds}", string.Join(", ", gpuIds));

                foreach (var gpuId in gpuIds)
                {
                    var newStatus = new GpuStatus
                    {
                        GpuId = gpuId,
                        CurrentModel = null,
                        LastActivity = DateTime.UtcNow
                    };
                    dataService.SetGpuStatus(gpuId, newStatus);
                }

                var unlockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "");
                subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(unlockedGpus));
                gpusFreedEvent.Set();
            }
        }

        public void RefreshGpuActivity(string[] gpuIds)
        {
            lock (GpuLockObject)
            {
                var currentTime = DateTime.UtcNow;
                foreach (var gpuId in gpuIds)
                {
                    var currentStatus = dataService.GetGpuStatus(gpuId);
                    if (currentStatus != null)
                    {
                        var updatedStatus = currentStatus with { LastActivity = currentTime };
                        dataService.SetGpuStatus(gpuId, updatedStatus);
                    }
                }
            }
        }

        public async Task KeepGpuRefreshAliveAsync(string[] gpuIds, CancellationToken cancelToken)
        {
            try
            {
                // Keep looping until the request is finished or canceled
                while (!cancelToken.IsCancellationRequested)
                {
                    // Call the "one call" RefreshGpuActivity method
                    this.RefreshGpuActivity(gpuIds);

                    // Wait for 20 seconds before calling it again
                    await Task.Delay(TimeSpan.FromSeconds(20), cancelToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Normal on cancellation
            }
            catch (Exception ex)
            {
                // Log errors so they aren’t swallowed
                logger.LogError(ex, "Error in KeepGpuRefreshAliveAsync for {GpuIds}", gpuIds);
            }
        }

        public async Task<ModelAssignment?> GetAvailableModelAssignmentAsync(OutputType outputType, string modelName, CancellationToken cancellationToken)
        {
            var modelAssignments = await dataService.GetModelAssignmentsAsync(outputType, modelName);
            if (!modelAssignments.Any())
                return null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (GpuLockObject)
                {
                    foreach (var modelAssignment in modelAssignments)
                    {
                        var gpuIds = modelAssignment.GpuIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToArray();
                        if (TryLockGPUs(gpuIds, modelName))
                        {
                            logger.LogDebug("GPU(s) {GpuIds} reserved, returning modelAssignments.", string.Join(',', gpuIds));
                            return modelAssignment;
                        }
                        else
                        {
                            logger.LogWarning("Failed to lock GPUs: {GpuIds}", string.Join(',', gpuIds));
                        }
                    }
                }

                // Wait for GPU freed notification or cancellation
                gpusFreedEvent.Wait(cancellationToken);
                gpusFreedEvent.Reset(); // Reset the event to wait for the next change
            }
        }

        public bool IsHealthy()
        {
            try
            {
                // Check if Redis connection is healthy
                var pingTimespan = subscriber.Ping();

                // Check if we can access the database
                var modelAssignments = dataService.GetAllGpuStatuses();

                // Consider the service healthy if we can reach here
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected virtual Dispose method for derived classes
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                gpusFreedEvent.Dispose();
            }
        }
    }
}
