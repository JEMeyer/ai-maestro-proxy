using System.Text.Json;
using AIMaestroProxy.Enums;
using AIMaestroProxy.Extensions;
using StackExchange.Redis;

namespace AIMaestroProxy.Services
{
    public class CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        public void CacheData(CacheCategory category, string identifier, string data)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"{category.ToCacheKey()}:{identifier}";
            logger.LogDebug("Storing serialized data to cache: {serializedData}", data);

            db.StringSet(cacheKey, data);
        }

        public async Task CacheDataAsync(
            CacheCategory category, string identifier, string data)
        {
            var db = redis.GetDatabase();
            var cacheKey = $"{category.ToCacheKey()}:{identifier}";
            logger.LogDebug("Storing serialized data to cache: {serializedData}", data);

            await db.StringSetAsync(cacheKey, data);
        }

        public T? GetCachedData<T>(CacheCategory category, string identifier) where T : class
        {
            var db = redis.GetDatabase();
            var cacheKey = $"{category.ToCacheKey()}:{identifier}";

            var cachedData = db.StringGet(cacheKey);
            if (cachedData.HasValue)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<T>(cachedData.ToString());
                    logger.LogDebug("Retrieved data from cache: {data}", JsonSerializer.Serialize(data));
                    return data;
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing data from cache - removing item from cache.");
                    db.KeyDelete(cacheKey);
                    return null;
                }
            }

            return null;
        }

        public async Task<T?> GetCachedDataAsync<T>(CacheCategory category, string identifier) where T : class
        {
            var db = redis.GetDatabase();
            var cacheKey = $"{category.ToCacheKey()}:{identifier}";

            var cachedData = await db.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<T>(cachedData.ToString());
                    logger.LogDebug("Retrieved data from cache: {data}", JsonSerializer.Serialize(data));
                    return data;
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing data from cache - removing item from cache.");
                    await db.KeyDeleteAsync(cacheKey);
                    return null;
                }
            }

            return null;
        }
    }
}
