using System.Text.Json;
using AIMaestroProxy.Interfaces;
using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.Cache;
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
        /// <param name="outputType">The service name to filter assignments.</param>
        /// <param name="modelName">Optional model name to further filter assignments.</param>
        /// <returns>A list of ModelAssignment objects.</returns>
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(OutputType outputType, string? modelName = null)
        {
            try
            {
                string cacheKey = GenerateCacheKey(outputType, modelName);
                _logger.LogDebug("Fetching ModelAssignments with key: {cacheKey}", cacheKey);

                var cachedData = _cacheService.GetCachedModelAssignments(cacheKey);

                if (cachedData != null && cachedData.Length > 0)
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
                    _logger.LogDebug("Fetching assignments by service: {OutputType}", outputType);
                    modelAssignments = await _databaseService.GetModelAssignmentByOutputTypeAsync(outputType);
                }

                if (modelAssignments.Any())
                {
                    string serializedData = JsonSerializer.Serialize(modelAssignments);
                    await _cacheService.SetCachedModelAssignmentsAsync(cacheKey, serializedData);
                    _logger.LogDebug("Cached data for key: {cacheKey}", cacheKey);
                }

                return modelAssignments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetModelAssignmentsAsync for service {OutputType} and model {modelName}", outputType, modelName);
                throw;
            }
        }

        /// <summary>
        /// Generates a standardized cache key
        /// </summary>
        private static string GenerateCacheKey(OutputType outputType, string? modelName)
        {
            return string.IsNullOrEmpty(modelName)
                ? $"{outputType.ToStorageName()}"
                : $"{outputType.ToStorageName()}:{modelName}";
        }


        /// <summary>
        /// Retrieves the GPU status for a given GPU ID from cache.
        /// </summary>
        /// <param name="gpuId">Id of the gpu to get the status for.</param>
        /// <returns>GpuStatus, or undefined</returns>
        public GpuStatus? GetGpuStatus(string gpuId)
        {
            return _cacheService.GetCachedGpuStatus(gpuId);
        }

        /// <summary>
        /// Sets the GPU status for a given GPU ID in cache.
        /// </summary>
        /// <param name="gpuId">Id of the gpu to set the status for.</param>
        /// <param name="status">New GpuStatus to put in the cache.</param>
        public void SetGpuStatus(string gpuId, GpuStatus status)
        {
            string serializedData = JsonSerializer.Serialize(status);
            _cacheService.SetCachedGpuStatus(gpuId, serializedData, TimeSpan.FromMinutes(10));
        }

        /// <summary>
        /// Retrieves all GPU statuses from cache.
        /// </summary>
        /// <returns>IEnumerable of GpuStatus objects. </returns>
        public IEnumerable<GpuStatus> GetAllGpuStatuses()
        {
            return _cacheService.GetAllCachedGpuStatuses();
        }

        public long ClearCache()
        {
            var gpuStatusCount = _cacheService.RemoveAllCachedGpuStatuses(CacheCategory.GpuStatus);
            var modelAssignmentsCount = _cacheService.RemoveAllCachedGpuStatuses(CacheCategory.ModelAssignments);

            return gpuStatusCount + modelAssignmentsCount;
        }
    }
}
