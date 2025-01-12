using System.Text.Json;
using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;


namespace AIMaestroProxy.Services
{
    public class DataService(CacheService cacheService, DatabaseService databaseService, ILogger<DataService> logger)
    {
        private readonly CacheService _cacheService = cacheService;
        private readonly DatabaseService _databaseService = databaseService;
        private readonly ILogger<DataService> _logger = logger;

        /// <summary>
        /// Retrieves model assignments based on service name and optionally model name.
        /// </summary>
        /// <param name="OutputType">The service name to filter assignments.</param>
        /// <param name="modelName">Optional model name to further filter assignments.</param>
        /// <returns>A list of ModelAssignment objects.</returns>
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(OutputType OutputType, string? modelName = null)
        {
            try
            {
                string cacheKey = GenerateCacheKey(OutputType, modelName);
                _logger.LogDebug("Fetching ModelAssignments with key: {cacheKey}", cacheKey);

                var cachedData = await _cacheService.GetCachedDataAsync<IEnumerable<ModelAssignment>>(CacheCategory.ModelAssignments, cacheKey);

                if (cachedData != null && cachedData.Any())
                {
                    _logger.LogDebug("Cache hit for key: {cacheKey}", cacheKey);
                    return cachedData;
                }

                _logger.LogDebug("Cache miss for key: {cacheKey}. Fetching from database.", cacheKey);
                IEnumerable<ModelAssignment> modelAssignments;

                if (!string.IsNullOrEmpty(modelName))
                {
                    _logger.LogDebug("Fetching assignments by model: {modelName}", modelName);
                    modelAssignments = await _databaseService.GetModelAssignmentsByModelAsync(modelName);
                }
                else
                {
                    _logger.LogDebug("Fetching assignments by service: {OutputType}", OutputType);
                    modelAssignments = await _databaseService.GetModelAssignmentByServiceAsync(OutputType);
                }

                if (modelAssignments.Any())
                {
                    string serializedData = JsonSerializer.Serialize(modelAssignments);
                    await _cacheService.CacheDataAsync(CacheCategory.ModelAssignments, cacheKey, serializedData);
                    _logger.LogDebug("Cached data for key: {cacheKey}", cacheKey);
                }

                return modelAssignments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetModelAssignmentsAsync for service {OutputType} and model {modelName}", OutputType, modelName);
                throw;
            }
        }

        /// <summary>
        /// Generates a standardized cache key based on service and model names.
        /// </summary>
        private static string GenerateCacheKey(OutputType OutputType, string? modelName)
        {
            return "model-assignments:" + (string.IsNullOrEmpty(modelName)
                ? $"{OutputType.ToFriendlyString()}"
                : $"{OutputType.ToFriendlyString()}:{modelName}");
        }

        public GpuLock? GetGpuLock(string gpuId)
        {
            return _cacheService.GetCachedData<GpuLock>(CacheCategory.GpuLock, gpuId);
        }

        public void SetGpuLock(string gpuId, GpuLock gpuLock)
        {
            _cacheService.CacheData(CacheCategory.GpuLock, gpuId, gpuLock, TimeSpan.FromMinutes(10));
        }
    }
}
