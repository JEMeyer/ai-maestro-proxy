using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class GpuManagerService(DataService dataService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger)
    {
        private readonly DataService dataService = dataService;
        private readonly ILogger<GpuManagerService> logger = logger;
        private readonly ISubscriber subscriber = redis.GetSubscriber();
        private readonly object lockObject = new();
        private readonly ManualResetEventSlim gpusFreedEvent = new(false);
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");

        public bool TryLockGPUs(string[] gpuIds)
        {
            try
            {
                logger.LogDebug("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var unlocked = gpuIds.All(gpuId =>
                        {
                            bool isUnlocked = !dataService.GetGpuLock(gpuId);
                            logger.LogDebug("GPU {GpuId} lock status: {LockStatus}", gpuId, isUnlocked ? "Unlocked" : "Locked");
                            return isUnlocked;
                        });
                if (unlocked)
                {
                    logger.LogDebug("Locking gpus : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        dataService.SetGpuLock(gpuId, true);
                    }
                    var lockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "1");
                    subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(lockedGpus));
                    return true;
                }
                logger.LogWarning("Failed to lock : {GpuIds}", string.Join(", ", gpuIds));
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TryLockGPUs for GPUs {GpuIds}", string.Join(", ", gpuIds));
                throw;
            }
        }

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (lockObject)
            {
                logger.LogDebug("Unlocking gpus  : {GpuIds}", string.Join(", ", gpuIds));
                foreach (var gpuId in gpuIds)
                {
                    dataService.SetGpuLock(gpuId, false);
                }
                var unlockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "0");
                var message = JsonSerializer.Serialize(unlockedGpus);
                subscriber.Publish(GpuLockChangesChannel, message);
                gpusFreedEvent.Set();
            }
        }

        public async Task<ModelAssignment?> GetAvailableModelAssignmentAsync(string modelName, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(modelName);
            var modelAssignments = await dataService.GetModelAssignmentsAsync(modelName);
            if (!modelAssignments.Any())
                return null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (lockObject)
                {
                    foreach (var modelAssignment in modelAssignments)
                    {
                        var gpuIds = modelAssignment.GpuIds.Split(',');
                        if (TryLockGPUs(gpuIds))
                        {
                            logger.LogDebug("GPUs {GpuIds} reserved, returning modelAssignments.", string.Join(',', gpuIds));
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
    }
}
