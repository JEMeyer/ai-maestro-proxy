using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class CacheService(IConnectionMultiplexer redis, ILogger<DatabaseService> _logger)
    {
        public async Task CacheAssignmentsAsync(string modelName, IEnumerable<Assignment> assignments)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var serializedAssignments = JsonSerializer.Serialize(assignments);
            _logger.LogDebug("Storing serialized assignments to cache: {serializedAssignments}", serializedAssignments);

            await db.StringSetAsync(cacheKey, serializedAssignments);
        }
        public async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var cachedAssignments = await db.StringGetAsync(cacheKey);
            if (cachedAssignments.HasValue)
            {
                try
                {
                    var assignments = JsonSerializer.Deserialize<IEnumerable<Assignment>>(cachedAssignments.ToString());
                    _logger.LogDebug("Retrieved assignments from cache: {assignments}", JsonSerializer.Serialize(assignments));
                    return assignments ?? [];
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing assignments from cache - removing item from cache.");
                    await db.KeyDeleteAsync(cacheKey); // Delete the corrupted cache entry
                    return [];
                }
            }

            return [];
        }
    }
}
