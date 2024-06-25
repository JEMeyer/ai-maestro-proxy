using System.Text.Json;
using ai_maestro_proxy.Models;
using StackExchange.Redis;

namespace ai_maestro_proxy.Services
{
    public class CacheService(IConnectionMultiplexer redis, ILogger<DatabaseService> _logger)
    {
        public async Task CacheAssignmentsAsync(string modelName, IEnumerable<Assignment> assignments)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var serializedAssignments = JsonSerializer.Serialize(assignments);
            _logger.LogInformation($"Storing serialized assignments to cache: {serializedAssignments}");

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
                    _logger.LogInformation("Retrieved assignments from cache: {assignments}", JsonSerializer.Serialize(assignments));
                    return assignments ?? [];
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing assignments from cache - removing item from cache.");
                    await db.KeyDeleteAsync(cacheKey); // Delete the corrupted cache entry
                }
            }

            return [];
        }
    }
}
