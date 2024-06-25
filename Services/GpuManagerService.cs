using ai_maestro_proxy.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ai_maestro_proxy.Services
{
    public class GpuManagerService(DatabaseService databaseService, CacheService cacheService, IConnectionMultiplexer redis, ILogger<GpuManagerService> _logger)
    {
        private readonly object _lockObject = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<Assignment>>> _modelQueues = new();

        public bool TryLockGPUs(string[] gpuIds)
        {
            _logger.LogInformation("Attempting to lock : {GpuIds}", string.Join(", ", gpuIds));
            var db = redis.GetDatabase();
            var unlocked = gpuIds.All(gpuId => db.StringGet($"gpu-lock-{gpuId}") == false);

            if (unlocked)
            {
                _logger.LogInformation("Locking gpus : {GpuIds}", string.Join(", ", gpuIds));
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock-{gpuId}", true);
                }
                return true;
            }
            _logger.LogInformation("Failed to lock : {GpuIds}", string.Join(", ", gpuIds));
            return false;
        }

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (_lockObject)
            {
                _logger.LogInformation("Unlocking gpus  : {GpuIds}", string.Join(", ", gpuIds));
                var db = redis.GetDatabase();
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock-{gpuId}", false);
                }
            }
        }

        private async Task ProcessQueues()
        {
            _logger.LogInformation("Processing Queues: {queues}", JsonSerializer.Serialize(_modelQueues));
            foreach (var modelQueue in _modelQueues)
            {
                var modelName = modelQueue.Key;
                var queue = modelQueue.Value;

                _logger.LogInformation("Processing queue for model: {modelName} with {queueCount} items", modelName, queue.Count);

                if (queue.TryPeek(out var tcs))
                {
                    _logger.LogInformation("Peeked at queue for model: {modelName}, attempting to fetch assignments", modelName);

                    var assignments = await GetAssignmentsAsync(modelName);
                    _logger.LogInformation("Fetched assignments for model {modelName}: {assignments}", modelName, JsonSerializer.Serialize(assignments));

                    lock (_lockObject)
                    {
                        foreach (var assignment in assignments)
                        {
                            var gpuIds = assignment.GpuIds.Split(',');
                            _logger.LogInformation("Attempting to lock GPUs: {GpuIds}", string.Join(',', gpuIds));

                            if (TryLockGPUs(gpuIds))
                            {
                                _logger.LogInformation("GPUs {GpuIds} reserved, returning assignment.", string.Join(',', gpuIds));
                                queue.TryDequeue(out _);
                                tcs.SetResult(assignment);
                                _logger.LogInformation("Assignment set for model {modelName}", modelName);
                                break;
                            }
                            else
                            {
                                _logger.LogInformation("Failed to lock GPUs: {GpuIds}", string.Join(',', gpuIds));
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Queue for model {modelName} is empty or unable to peek", modelName);
                }
            }
        }

        private async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            // Try to get the assignment from the cache
            var cachedAssignments = await cacheService.GetAssignmentsAsync(modelName);
            if (cachedAssignments.Any()) { return cachedAssignments; }

            var assignments = await databaseService.GetAssignmentsForModelAsync(modelName);
            if (assignments.Any())
            {
                // Cache the result
                await cacheService.CacheAssignmentsAsync(modelName, assignments);
            }

            return assignments;
        }

        public async Task<Assignment> GetAvailableAssignmentAsync(string modelName)
        {
            _logger.LogInformation("About to queue {model}", modelName);

            var tcs = new TaskCompletionSource<Assignment>();
            var queue = _modelQueues.GetOrAdd(modelName, _ => new ConcurrentQueue<TaskCompletionSource<Assignment>>());

            // Log before enqueuing
            _logger.LogInformation("Queue before enqueue for model {model}: {queue}", modelName, JsonSerializer.Serialize(queue));

            // Enqueue the TaskCompletionSource
            queue.Enqueue(tcs);

            // Log after enqueuing
            _logger.LogInformation("Enqueued {model}. Queue after enqueue for model {model}: {queueCount} items", modelName, modelName, queue.Count);

            // Try to process the queue immediately
            try
            {
                _logger.LogInformation("Starting to process queues for model {modelName}", modelName);
                await ProcessQueues();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queues.");
            }

            // Log the queue after processing
            _logger.LogInformation("Queue after processing for model {model}: {queueCount} items", modelName, queue.Count);

            return await tcs.Task;
        }
    }
}
