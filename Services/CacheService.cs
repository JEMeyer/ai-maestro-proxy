using System.Text.Json;
using ai_maestro_proxy.Models;
using StackExchange.Redis;

namespace ai_maestro_proxy.Services
{
    public class CacheService(IConnectionMultiplexer redis)
    {
        public async Task CacheAssignmentsAsync(string modelName, IEnumerable<Assignment> assignments)
        {
            var db = redis.GetDatabase();
            foreach (var assignment in assignments)
            {
                await db.StringSetAsync($"model-assignments:{modelName}", JsonSerializer.Serialize(assignment));
            }
        }

        public async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"model-assignments:{modelName}";

            var cachedAssignments = await db.StringGetAsync(cacheKey);
            if (cachedAssignments.HasValue)
            {
                return JsonSerializer.Deserialize<IEnumerable<Assignment>>(cachedAssignments.ToString()) ?? new List<Assignment>();
            }
            return [];
        }
    }
}
