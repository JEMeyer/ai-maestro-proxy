using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        public async Task CacheModelAssignmentsAsync(string modelName, IEnumerable<ModelAssignment> modelAssignments)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var serializedModelAssignments = JsonSerializer.Serialize(modelAssignments);
            logger.LogDebug("Storing serialized modelAssignments to cache: {serializedModelAssignments}", serializedModelAssignments);

            await db.StringSetAsync(cacheKey, serializedModelAssignments);
        }

        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(string modelName)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var cachedModelAssignments = await db.StringGetAsync(cacheKey);
            if (cachedModelAssignments.HasValue)
            {
                try
                {
                    var modelAssignments = JsonSerializer.Deserialize<IEnumerable<ModelAssignment>>(cachedModelAssignments.ToString());
                    logger.LogDebug("Retrieved modelAssignments from cache: {modelAssignments}", JsonSerializer.Serialize(modelAssignments));
                    return modelAssignments ?? [];
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing modelAssignments from cache - removing item from cache.");
                    await db.KeyDeleteAsync(cacheKey); // Delete the corrupted cache entry
                    return [];
                }
            }

            return [];
        }
    }
}
