using ai_maestro_proxy.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace ai_maestro_proxy.Services
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
                _logger.LogInformation("Setting the reset event to free queued threads");
                _gpusFreedEvent.Set();
            });
        }

        private void PublishNewGpusAvailableNotification(string[] gpuIds)
        {
            _logger.LogInformation("Publishing to GPU Available Channel");
            _subscriber.Publish(NewGpuAvailableChannel, $"GPU(s) {gpuIds} freed");
        }

        private async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            try
            {
                _logger.LogInformation("Getting assignments for model: {modelName}", modelName);
                var cachedAssignments = await _cacheService.GetAssignmentsAsync(modelName);
                if (cachedAssignments.Any())
                {
                    _logger.LogInformation("Found cached assignments for model: {modelName}", modelName);
                    return cachedAssignments;
                }

                var assignments = await _databaseService.GetAssignmentsForModelAsync(modelName);
                if (assignments.Any())
                {
                    _logger.LogInformation("Caching assignments for model: {modelName}", modelName);
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
                _logger.LogInformation("Attempting to lock : {GpuIds}", string.Join(", ", gpuIds));
                var db = _redis.GetDatabase();
                var unlocked = gpuIds.All(gpuId =>
                        {
                            var value = db.StringGet($"gpu-lock:{gpuId}");
                            bool isUnlocked = value.IsNull || value == (RedisValue)false;
                            _logger.LogInformation("GPU {GpuId} lock status: {LockStatus}", gpuId, isUnlocked ? "Unlocked" : "Locked");
                            return isUnlocked;
                        });
                if (unlocked)
                {
                    _logger.LogInformation("Locking gpus : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        db.StringSet($"gpu-lock:{gpuId}", true);
                    }
                    return true;
                }
                _logger.LogInformation("Failed to lock : {GpuIds}", string.Join(", ", gpuIds));
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
                _logger.LogInformation("Unlocking gpus  : {GpuIds}", string.Join(", ", gpuIds));
                var db = _redis.GetDatabase();
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock:{gpuId}", false);
                }
                PublishNewGpusAvailableNotification(gpuIds);
            }
        }

        public async Task<Assignment?> GetAvailableAssignmentAsync(string modelName, CancellationToken cancellationToken)
        {
            var assignments = await GetAssignmentsAsync(modelName);
            _logger.LogInformation("Fetched assignments for model {modelName}: {assignments}", modelName, JsonSerializer.Serialize(assignments));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    foreach (var assignment in assignments)
                    {
                        var gpuIds = assignment.GpuIds.Split(',');
                        _logger.LogInformation("Attempting to lock GPUs: {GpuIds}", string.Join(',', gpuIds));

                        if (TryLockGPUs(gpuIds))
                        {
                            _logger.LogInformation("GPUs {GpuIds} reserved, returning assignment.", string.Join(',', gpuIds));
                            return assignment;
                        }
                        else
                        {
                            _logger.LogInformation("Failed to lock GPUs: {GpuIds}", string.Join(',', gpuIds));
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
