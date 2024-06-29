using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class GpuManagerService
    {
        private readonly DataService dataService;
        private readonly IConnectionMultiplexer redis;
        private readonly ILogger<GpuManagerService> logger;
        private readonly ISubscriber subscriber;
        private readonly object lockObject = new();
        private readonly ManualResetEventSlim gpusFreedEvent = new(false);
        private static readonly RedisChannel NewGpuAvailableChannel = RedisChannel.Literal("gpu-now-free-channel");
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");

        public GpuManagerService(DataService dataService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger)
        {
            this.dataService = dataService;
            this.redis = redis;
            this.logger = logger;
            subscriber = redis.GetSubscriber();
            SubscribeToNewGpuAvailableNotifications();
        }

        private void SubscribeToNewGpuAvailableNotifications()
        {
            subscriber.Subscribe(NewGpuAvailableChannel, (channel, message) =>
            {
                logger.LogDebug("Setting the reset event to free queued threads");
                gpusFreedEvent.Set();
            });
        }

        private async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(string modelName)
        {
            try
            {
                logger.LogDebug("Getting modelAssignments for model: {modelName}", modelName);
                var cachedModelAssignments = await dataService.GetModelAssignmentsAsync(modelName);
                if (cachedModelAssignments.Any())
                {
                    logger.LogDebug("Found cached modelAssignments for model: {modelName}", modelName);
                    return cachedModelAssignments;
                }

                var modelAssignments = await dataService.GetModelAssignmentsAsync(modelName);
                if (modelAssignments.Any())
                {
                    logger.LogDebug("Found modelAssignments for model: {modelName}, saving to cache.", modelName);
                    await dataService.CacheModelAssignmentsAsync(modelName, modelAssignments);
                }

                return modelAssignments;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetModelAssignmentsAsync for model {modelName}", modelName);
                throw;
            }
        }

        public bool TryLockGPUs(string[] gpuIds)
        {
            try
            {
                logger.LogDebug("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var db = redis.GetDatabase();
                var unlocked = gpuIds.All(gpuId =>
                        {
                            var value = db.StringGet($"gpu-lock:{gpuId}");
                            bool isUnlocked = value.IsNull || value == (RedisValue)false;
                            logger.LogDebug("GPU {GpuId} lock status: {LockStatus}", gpuId, isUnlocked ? "Unlocked" : "Locked");
                            return isUnlocked;
                        });
                if (unlocked)
                {
                    logger.LogDebug("Locking gpus : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        db.StringSet($"gpu-lock:{gpuId}", true);
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
                var db = redis.GetDatabase();
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock:{gpuId}", false);
                }
                subscriber.Publish(NewGpuAvailableChannel, $"GPU(s) {gpuIds} freed");
                var lockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "0");
                subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(lockedGpus));
            }
        }

        public async Task<ModelAssignment?> GetAvailableModelAssignmentAsync(string modelName, CancellationToken cancellationToken)
        {
            var modelAssignments = await GetModelAssignmentsAsync(modelName);

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
