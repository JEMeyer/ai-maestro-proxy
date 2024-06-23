using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace ai_maestro_proxy.Services
{
    public class CacheService(string connectionString)
    {
        private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(connectionString);

        public async Task<string> GetCachedValueAsync(string key)
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            return value.ToString() ?? string.Empty;
        }

        public async Task SetCachedValueAsync(string key, string value, TimeSpan expiration)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value, expiration);
        }
    }
}
