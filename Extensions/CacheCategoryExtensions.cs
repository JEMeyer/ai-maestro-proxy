using AIMaestroProxy.Enums;

namespace AIMaestroProxy.Extensions
{
    public static class CacheCategoryExtensions
    {
        public static string ToCacheKey(this CacheCategory category)
        {
            return category switch
            {
                CacheCategory.ContainerInfos => "container-infos",
                CacheCategory.GpuLock => "gpu-lock",
                CacheCategory.ModelAssignments => "model-assignments",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }
    }
}
