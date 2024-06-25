using ai_maestro_proxy.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace ai_maestro_proxy.Services
{
    public class GpuManagerService(DatabaseService databaseService, CacheService cacheService, IConnectionMultiplexer redis)
    {
        private readonly object _lockObject = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<Assignment>>> _modelQueues = new();

        public bool TryLockGPUs(string[] gpuIds)
        {
            var db = redis.GetDatabase();
            var unlocked = gpuIds.All(gpuId => db.StringGet($"gpu-lock-{gpuId}") == false);

            if (unlocked)
            {
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock-{gpuId}", true);
                }
                return true;
            }
            return false;
        }

        public void UnlockGPUs(string[] gpuIds)
        {
            lock (_lockObject)
            {
                var db = redis.GetDatabase();
                foreach (var gpuId in gpuIds)
                {
                    db.StringSet($"gpu-lock-{gpuId}", false);
                }
            }
        }

        private async void ProcessQueues()
        {

            foreach (var modelQueue in _modelQueues)
            {
                var modelName = modelQueue.Key;
                var queue = modelQueue.Value;

                if (queue.TryPeek(out var tcs))
                {
                    var assignments = await GetAssignmentsAsync(modelName);
                    lock (_lockObject)
                    {
                        foreach (var assignment in assignments)
                        {
                            var gpuIds = assignment.GpuIds.Split(',');
                            if (TryLockGPUs(gpuIds))
                            {
                                queue.TryDequeue(out _);
                                tcs.SetResult(assignment);
                                break;
                            }
                        }
                    }
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
            var tcs = new TaskCompletionSource<Assignment>();

            var queue = _modelQueues.GetOrAdd(modelName, _ => new ConcurrentQueue<TaskCompletionSource<Assignment>>());
            queue.Enqueue(tcs);

            // Try to process the queue immediately
            ProcessQueues();

            return await tcs.Task;
        }
    }
}
