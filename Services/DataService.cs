using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class DataService(CacheService cacheService, DatabaseService databaseService, ILogger<DataService> logger)
    {
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(string modelName)
        {
            try
            {
                logger.LogDebug("Getting modelAssignments for model: {modelName}", modelName);
                var cachedModelAssignments = await cacheService.GetModelAssignmentsAsync(modelName);
                if (cachedModelAssignments.Any())
                {
                    logger.LogDebug("Found cached modelAssignments for model: {modelName}", modelName);
                    return cachedModelAssignments;
                }

                var modelAssignments = await databaseService.GetModelAssignmentsAsync(modelName);
                if (modelAssignments.Any())
                {
                    logger.LogDebug("Found modelAssignments for model: {modelName}, saving to cache.", modelName);
                    await cacheService.CacheModelAssignmentsAsync(modelName, modelAssignments);
                }

                return modelAssignments;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GetModelAssignmentsAsync for model {modelName}", modelName);
                throw;
            }
        }

        public async Task<IEnumerable<ContainerInfo>> GetLlmContainerInfosAsync()
        {
            var containerInfos = await databaseService.GetLlmContainerInfosAsync();
            return containerInfos;
        }

        public async Task<IEnumerable<ContainerInfo>> GetDiffusionContainerInfosAsync()
        {
            var containerInfos = await databaseService.GetDiffusionContainerInfosAsync();
            return containerInfos;
        }


        public async Task CacheModelAssignmentsAsync(string modelName, IEnumerable<ModelAssignment> modelAssignments)
        {
            await cacheService.CacheModelAssignmentsAsync(modelName, modelAssignments);
        }
    }
}
