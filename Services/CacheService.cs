using StackExchange.Redis;

namespace ai_maestro_proxy.Services
{
    public class CacheService(string connectionString)
    {
        private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(connectionString);

        public async Task<string> GetCachedValueAsync(string key)
        {
            IDatabase db = _redis.GetDatabase();
            RedisValue value = await db.StringGetAsync(key);
            return value.ToString() ?? string.Empty;
        }

        public async Task SetCachedValueAsync(string key, string value, TimeSpan expiration)
        {
            IDatabase db = _redis.GetDatabase();
            await db.StringSetAsync(key, value, expiration);
        }

        public async Task ClearCachedValueAsync(string key)
        {
            IDatabase db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
    }
}
