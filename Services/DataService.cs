using System.Text.Json;
using AIMaestroProxy.Enums;
using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;


namespace AIMaestroProxy.Services
{
    public class DataService(CacheService cacheService, DatabaseService databaseService, ILogger<DataService> logger)
    {
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(string modelName)
        {
            try
            {
                logger.LogDebug("Getting modelAssignments for model: {modelName}", modelName);
                var cachedModelAssignments = await cacheService.GetCachedDataAsync<IEnumerable<ModelAssignment>>(CacheCategory.ModelAssignments, modelName);
                if (cachedModelAssignments != null && cachedModelAssignments.Any())
                {
                    return cachedModelAssignments;
                }

                var modelAssignments = await databaseService.GetModelAssignmentsAsync(modelName);
                if (modelAssignments.Any())
                {
                    await cacheService.CacheDataAsync(CacheCategory.ModelAssignments, modelName, JsonSerializer.Serialize(modelAssignments));
                }

                return modelAssignments;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetModelAssignmentsAsync for model {modelName}", modelName);
                throw;
            }
        }

        public async Task<IEnumerable<ContainerInfo>> GetContainerInfosAsync(PathFamily pathFamily)
        {
            try
            {
                logger.LogDebug("Getting containerInfos for family: {fam}", pathFamily);
                string pathFamilyString = pathFamily.ToString();
                var cahcedContainerInfos = await cacheService.GetCachedDataAsync<IEnumerable<ContainerInfo>>(CacheCategory.ContainerInfos, pathFamilyString);
                if (cahcedContainerInfos != null && cahcedContainerInfos.Any())
                {
                    return cahcedContainerInfos;
                }

                var containerInfos = await databaseService.GetContainerInfosAsync(pathFamily);
                if (containerInfos.Any())
                {
                    await cacheService.CacheDataAsync(CacheCategory.ContainerInfos, pathFamilyString, JsonSerializer.Serialize(containerInfos));
                }

                return containerInfos;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetContainerInfosAsync for family {modelName}", pathFamily);
                throw;
            }
        }

        public bool GetGpuLock(string gpuId)
        {
            return bool.Parse(cacheService.GetCachedData<string>(CacheCategory.GpuLock, gpuId) ?? "False");
        }

        public void SetGpuLock(string gpuId, bool lockState)
        {
            cacheService.CacheData(CacheCategory.GpuLock, gpuId, lockState.ToString());
        }
    }
}
