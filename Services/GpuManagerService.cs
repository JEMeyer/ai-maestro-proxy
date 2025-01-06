using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace AIMaestroProxy.Services
{
    public partial class GpuManagerService : IDisposable
    {
        private readonly DataService dataService;
        private readonly ILogger<GpuManagerService> logger;
        private readonly ISubscriber subscriber;
        private readonly object lockObject = new();
        private readonly ManualResetEventSlim gpusFreedEvent = new(false);
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");
        private readonly ConcurrentDictionary<string, DateTime> _gpuLastActivity = new();
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1);
        private readonly Timer? _timer;

        public GpuManagerService(DataService dataService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger)
        {
            this.dataService = dataService;
            this.logger = logger;
            this.subscriber = redis.GetSubscriber();

            // Start a timer that checks every 30 seconds
            _timer = new Timer(_ => ReleaseStuckGPUs(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Runs every 30 seconds to release any gpus that are stuck - if inactive for more than 1 min will be released
        /// </summary>
        private void ReleaseStuckGPUs()
        {
            lock (lockObject)
            {
                var expiredGPUs = _gpuLastActivity.Where(pair => DateTime.UtcNow - pair.Value > _timeout).ToList();
                foreach (var gpu in expiredGPUs)
                {
                    // Logic to mark GPU as free
                    UnlockGPUs([gpu.Key]);
                    _gpuLastActivity.TryRemove(gpu.Key, out _);
                }
            }
        }

        // Returns false if no gpu was available, otherwise true to indicate you locked it
        private bool TryLockGPUs(string[] gpuIds, string modelName)
        {
            try
            {
                logger.LogDebug("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var unlocked = gpuIds.All(gpuId =>
                {
                    var gpuLock = dataService.GetGpuLock(gpuId);
                    var isUnlocked = gpuLock == null || gpuLock.ModelInUse == string.Empty;
                    logger.LogDebug("GPU {GpuId} lock status: {LockStatus}", gpuId, isUnlocked ? "Unlocked" : "Locked");
                    return isUnlocked;
                });
                if (unlocked)
                {
                    logger.LogDebug("Locking : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        dataService.SetGpuLock(gpuId, new GpuLock { ModelInUse = modelName });
                        _gpuLastActivity[gpuId] = DateTime.UtcNow;
                    }
                    var lockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => modelName);
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
            lock (lockObject)
            {
                logger.LogDebug("Unlocking gpus : {GpuIds}", string.Join(", ", gpuIds));
                foreach (var gpuId in gpuIds)
                {
                    dataService.SetGpuLock(gpuId, new GpuLock { ModelInUse = string.Empty });
                }
                var unlockedGpus = gpuIds.ToDictionary(gpuId => gpuId, _ => "");
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

        public void RefreshGpuActivity(string[] gpuIds)
        {
            lock (lockObject)
            {
                foreach (var gpuId in gpuIds)
                {
                    _gpuLastActivity[gpuId] = DateTime.UtcNow;
                }
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
                _timer?.Dispose();
                gpusFreedEvent.Dispose();
            }
        }
    }
}
