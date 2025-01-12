using System.Text.Json;
using AIMaestroProxy.Models;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class CacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<T?> GetCachedDataAsync<T>(CacheCategory category, string key)
        {
            var db = _redis.GetDatabase();
            var serializedData = await db.StringGetAsync(GetRedisKey(category, key));
            if (serializedData.IsNullOrEmpty)
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(serializedData!);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache data for key: {Key}", key);
                return default;
            }
        }

        public async Task CacheDataAsync<T>(CacheCategory category, string key, T data, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            string serializedData = JsonSerializer.Serialize(data);
            await db.StringSetAsync(GetRedisKey(category, key), serializedData, expiry);
        }

        public T? GetCachedData<T>(CacheCategory category, string key)
        {
            var db = _redis.GetDatabase();
            var serializedData = db.StringGet(GetRedisKey(category, key));
            if (serializedData.IsNullOrEmpty)
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(serializedData!);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache data for key: {Key}", key);
                return default;
            }
        }

        public void CacheData<T>(CacheCategory category, string key, T data, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            string serializedData = JsonSerializer.Serialize(data);
            db.StringSet(GetRedisKey(category, key), serializedData, expiry);
        }

        private string GetRedisKey(CacheCategory category, string key)
        {
            return $"{category}:{key}";
        }
    }
}
