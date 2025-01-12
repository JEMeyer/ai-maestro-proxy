// Services/GpuManagerService.cs
using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using static AIMaestroProxy.Models.PathCategories;
using AIMaestroProxy.Interfaces;

namespace AIMaestroProxy.Services
{
    public partial class GpuManagerService : IGpuManagerService
    {
        private readonly DatabaseService databaseService;
        private readonly DataService dataService;
        private readonly ILogger<GpuManagerService> logger;
        private readonly ISubscriber subscriber;
        private readonly object lockObject = new();
        private readonly ManualResetEventSlim gpusFreedEvent = new(false);
        private static readonly RedisChannel GpuLockChangesChannel = RedisChannel.Literal("gpu-lock-changes");
        private readonly ConcurrentDictionary<string, DateTime> _gpuLastActivity = new();
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1);
        private readonly Timer? _timer;

        public GpuManagerService(DatabaseService databaseService, DataService dataService, IConnectionMultiplexer redis, ILogger<GpuManagerService> logger)
        {
            this.databaseService = databaseService;
            this.dataService = dataService;
            this.logger = logger;
            subscriber = redis.GetSubscriber();

            // Start a timer that checks every 30 seconds
            _timer = new Timer(_ => ReleaseStuckGPUs(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// When ran, will release any GPUs that are stuck (inactive for more than 1 min)
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

        // Returns false if no GPU was available, otherwise true to indicate you locked it
        private bool TryLockGPUs(string[] gpuIds, string modelName)
        {
            try
            {
                logger.LogDebug("Checking/Locking : {GpuIds}", string.Join(", ", gpuIds));
                var unlocked = gpuIds.All(gpuId =>
                {
                    var gpuStatus = dataService.GetGpuStatus(gpuId);
                    if (gpuStatus == null)
                    {
                        logger.LogWarning("{GpuIds}: No gpu status to read", string.Join(", ", gpuIds));
                        return false;
                    }

                    logger.LogDebug("GPU {GpuId} lock status: {LockStatus}", gpuId, gpuStatus.IsLocked.ToString());
                    return !gpuStatus.IsLocked;
                });
                if (unlocked)
                {
                    logger.LogDebug("Locking : {GpuIds}", string.Join(", ", gpuIds));
                    foreach (var gpuId in gpuIds)
                    {
                        dataService.SetGpuStatus(gpuId, new GpuStatus { CurrentModel = modelName, GpuId = gpuId, IsLocked = true  });
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

        /*
        public string GpuId { get; init; } = string.Empty;
        public string? CurrentModel { get; init; }
        public DateTime? LastActivity { get; init; }
        public bool IsLocked => !string.IsNullOrEmpty(CurrentModel);
        */

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (lockObject)
            {
                logger.LogDebug("Unlocking GPUs : {GpuIds}", string.Join(", ", gpuIds));
                foreach (var gpuId in gpuIds)
                {
                    dataService.SetGpuStatus(gpuId, new GpuStatus { CurrentModel = string.Empty, LastActivity = DateTime.UtcNow, GpuId = gpuId });
                }
                var unlockedGpus = gpuIds.ToDictionary(gpuId => gpuId, gpuId => "");
                var message = JsonSerializer.Serialize(unlockedGpus);
                subscriber.Publish(GpuLockChangesChannel, message);
                gpusFreedEvent.Set();
            }
        }

        public async Task<ModelAssignment?> GetAvailableModelAssignmentAsync(OutputType outputType, string? modelName, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(modelName);
            var modelAssignments = await dataService.GetModelAssignmentsAsync(outputType, modelName);
            if (!modelAssignments.Any())
                return null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (lockObject)
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

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Check if Redis connection is healthy
                var pingTimespan = subscriber.Ping();

                // Check if we can access the database
                var modelAssignments = await databaseService.GetModelAssignmentByServiceAsync(OutputType.Text);

                // Consider the service healthy if we can reach both Redis and the database
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        public IEnumerable<GpuStatus> GetCurrentGpuStatus()
        {
            lock (lockObject)
            {
                var statuses = new List<GpuStatus>();

                // Convert the current GPU activity tracking into status objects
                foreach (var activity in _gpuLastActivity)
                {
                    var gpuLock = dataService.GetGpuStatus(activity.Key);
                    statuses.Add(new GpuStatus
                    {
                        GpuId = activity.Key,
                        CurrentModel = gpuLock?.CurrentModel,
                        LastActivity = activity.Value
                    });
                }

                return statuses;
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
