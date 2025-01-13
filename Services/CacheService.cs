using System.Text.Json;
using AIMaestroProxy.Interfaces;
using AIMaestroProxy.Models;
using StackExchange.Redis;
using static AIMaestroProxy.Models.Cache;

namespace AIMaestroProxy.Services
{
    public class CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly ILogger<CacheService> _logger = logger;

        public ModelAssignment[] GetCachedModelAssignments(string key)
        {
            var db = _redis.GetDatabase();
            var serializedData = db.StringGet(GetRedisKey(CacheCategory.ModelAssignments, key));
            if (serializedData.IsNullOrEmpty)
                return [];

            try
            {
                return JsonSerializer.Deserialize<ModelAssignment[]>(serializedData!) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache data for key: {Key}", key);
                return [];
            }
        }

        public async Task SetCachedModelAssignmentsAsync(string key, string data, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(GetRedisKey(CacheCategory.ModelAssignments, key), data, expiry);
        }

        public GpuStatus? GetCachedGpuStatus(string key)
        {
            var db = _redis.GetDatabase();
            var serializedData = db.StringGet(GetRedisKey(CacheCategory.GpuStatus, key));
            if (serializedData.IsNullOrEmpty)
                return default;

            _logger.LogInformation("gpustat " + serializedData);
            try
            {
                return JsonSerializer.Deserialize<GpuStatus>(serializedData!);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache data for key: {Key}", key);
                return default;
            }
        }

        public List<GpuStatus> GetAllCachedGpuStatuses()
        {
            var db = _redis.GetDatabase();
            var pattern = $"{CacheCategory.GpuStatus.ToFriendlyString()}:*";
            var keys = new List<RedisKey>();
            long cursor = 0;
            do
            {
                var scanResult = db.Execute("SCAN", cursor.ToString(), "MATCH", pattern, "COUNT", "100");
                if (scanResult.IsNull || scanResult.Resp2Type != ResultType.Array)
                {
                    _logger.LogError("Unexpected SCAN response format");
                    break;
                }

                RedisResult[]? innerResult;
                try
                {
                    innerResult = (RedisResult[])scanResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cast scan result to array");
                    break;
                }

                if (innerResult == null || innerResult.Length < 2 || innerResult[0].IsNull)
                {
                    _logger.LogError("Invalid SCAN response structure");
                    break;
                }

                var cursorStr = innerResult[0].ToString();
                if (string.IsNullOrEmpty(cursorStr) || !long.TryParse(cursorStr, out cursor))
                {
                    _logger.LogError("Invalid cursor value in SCAN response");
                    break;
                }

                RedisResult[]? keysArray;
                try
                {
                    keysArray = (RedisResult[])innerResult[1];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cast keys result to array");
                    break;
                }

                if (keysArray != null)
                {
                    var resultKeys = keysArray
                        .Where(x => x != null && !x.IsNull)
                        .Select(x => (RedisKey)x.ToString());
                    keys.AddRange(resultKeys);
                }
            } while (cursor != 0);

            if (keys.Count == 0)
            {
                _logger.LogDebug("No GPU status keys currently in Redis");
                return [];
            }

            var gpuStatuses = new List<GpuStatus>();
            var values = db.StringGet(keys.ToArray());

            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].IsNullOrEmpty)
                {
                    try
                    {
                        _logger.LogInformation("values[i] " + values[i]);
                        var status = JsonSerializer.Deserialize<GpuStatus>(values[i]!);
                        if (status != null)
                        {
                            gpuStatuses.Add(status);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize cache data for key: {Key}", keys[i]);
                    }
                }
            }

            return gpuStatuses;
        }

        public long RemoveAllCachedGpuStatuses(CacheCategory cacheCategory)
        {
            var db = _redis.GetDatabase();
            var pattern = $"{cacheCategory.ToFriendlyString()}:*";
            var keys = new List<RedisKey>();
            long cursor = 0;
            do
            {
                var scanResult = db.Execute("SCAN", cursor.ToString(), "MATCH", pattern, "COUNT", "100");
                if (scanResult.IsNull || scanResult.Resp2Type != ResultType.Array)
                {
                    _logger.LogError("Unexpected SCAN response format");
                    break;
                }

                RedisResult[]? innerResult;
                try
                {
                    innerResult = (RedisResult[])scanResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cast scan result to array");
                    break;
                }

                if (innerResult == null || innerResult.Length < 2 || innerResult[0].IsNull)
                {
                    _logger.LogError("Invalid SCAN response structure");
                    break;
                }

                var cursorStr = innerResult[0].ToString();
                if (string.IsNullOrEmpty(cursorStr) || !long.TryParse(cursorStr, out cursor))
                {
                    _logger.LogError("Invalid cursor value in SCAN response");
                    break;
                }

                RedisResult[]? keysArray;
                try
                {
                    keysArray = (RedisResult[])innerResult[1];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cast keys result to array");
                    break;
                }

                if (keysArray != null)
                {
                    var resultKeys = keysArray
                        .Where(x => x != null && !x.IsNull)
                        .Select(x => (RedisKey)x.ToString());
                    keys.AddRange(resultKeys);
                }
            } while (cursor != 0);

            if (keys.Count == 0)
            {
                _logger.LogDebug("No GPU status keys found to remove");
                return 0;
            }

            return db.KeyDelete([.. keys]);
        }

        public void SetCachedGpuStatus(string key, string data, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            db.StringSet(GetRedisKey(CacheCategory.GpuStatus, key), data, expiry);
        }

        private static string GetRedisKey(CacheCategory category, string key)
        {
            return $"{category.ToFriendlyString()}:{key}";
        }
    }
}
