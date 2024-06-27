using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class GpuManagerService
    {
        private readonly DatabaseService _databaseService;
        private readonly CacheService _cacheService;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<GpuManagerService> _logger;
        private readonly ISubscriber _subscriber;
        private readonly object _lockObject = new();
        private readonly ManualResetEventSlim _gpusFreedEvent = new(false);
        private static readonly RedisChannel NewGpuAvailableChannel = RedisChannel.Literal("gpu-now-free-channel");
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");

        public GpuManagerService(DatabaseService databaseService, CacheService cacheService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger)
        {
            _databaseService = databaseService;
            _cacheService = cacheService;
            _redis = redis;
            _logger = logger;
            _subscriber = redis.GetSubscriber();
            SubscribeToNewGpuAvailableNotifications();
        }
        private void SubscribeToNewGpuAvailableNotifications()
        {
            _subscriber.Subscribe(NewGpuAvailableChannel, (channel, message) =>
            {
                _logger.LogDebug("Setting the reset event to free queued threads");
                _gpusFreedEvent.Set();
            });
        }

        private async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            try
            {
                _logger.LogDebug("Getting assignments for model: {modelName}", modelName);
                var cachedAssignments = await _cacheService.GetAssignmentsAsync(modelName);
                if (cachedAssignments.Any())
                {
                    _logger.LogDebug("Found cached assignments for model: {modelName}", modelName);
                    return cachedAssignments;
                }

                var assignments = await _databaseService.GetAssignmentsForModelAsync(modelName);
                if (assignments.Any())
                {
                    _logger.LogDebug("Found assignments for model: {modelName}, saving to cache.", modelName);
                    await _cacheService.CacheAssignmentsAsync(modelName, assignments);
                }

                return assignments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAssignmentsAsync for model {modelName}", modelName);
                throw;
            }
        }

        public bool TryLockGPUs(string[] gpuIds)
        {
            try
            {
                _logger.LogDebug("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var db = _redis.GetDatabase();
                var unlocked = gpuIds.All(gpuId =>
                        {
                            var value = db.StringGet($"gpu-lock:{gpuId}");
                            bool isUnlocked = value.IsNull || value == (RedisValue)false;
                            _logger.LogDebug("GPU {GpuId} lock status: {LockStatus}", gpuId, isUnlocked ? "Unlocked" : "Locked");
                            return isUnlocked;
                        });
                if (unlocked)
                {
                    _logger.LogDebug("Locking gpus : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        db.StringSet($"gpu-lock:{gpuId}", true);
                    }
                    var lockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "1");
                    _subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(lockedGpus));
                    return true;
                }
                _logger.LogWarning("Failed to lock : {GpuIds}", string.Join(", ", gpuIds));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TryLockGPUs for GPUs {GpuIds}", string.Join(", ", gpuIds));
                throw;
            }
        }

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (_lockObject)
            {
                _logger.LogDebug("Unlocking gpus  : {GpuIds}", string.Join(", ", gpuIds));
                var db = _redis.GetDatabase();
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock:{gpuId}", false);
                }
                _subscriber.Publish(NewGpuAvailableChannel, $"GPU(s) {gpuIds} freed");
                var lockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "0");
                _subscriber.Publish(GpuLockChangesChannel, JsonSerializer.Serialize(lockedGpus));
            }
        }

        public async Task<Assignment?> GetAvailableAssignmentAsync(string modelName, CancellationToken cancellationToken)
        {
            var assignments = await GetAssignmentsAsync(modelName);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    foreach (var assignment in assignments)
                    {
                        var gpuIds = assignment.GpuIds.Split(',');
                        if (TryLockGPUs(gpuIds))
                        {
                            _logger.LogDebug("GPUs {GpuIds} reserved, returning assignment.", string.Join(',', gpuIds));
                            return assignment;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to lock GPUs: {GpuIds}", string.Join(',', gpuIds));
                        }
                    }
                }

                // Wait for GPU freed notification or cancellation
                _gpusFreedEvent.Wait(cancellationToken);
                _gpusFreedEvent.Reset(); // Reset the event to wait for the next change
            }
        }
    }
}
