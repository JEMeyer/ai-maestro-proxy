namespace AIMaestroProxy.Models
{
    public static class Cache
    {
        public enum CacheCategory
        {
            GpuStatus,
            ModelAssignments
        }

        public static string ToFriendlyString(this CacheCategory cacheCategory)
        {
            return cacheCategory switch
            {
                CacheCategory.GpuStatus => "gpu-status",
                CacheCategory.ModelAssignments => "model-assignments",
                _ => throw new ArgumentException("Invalid path family.")
            };
        }
    }
}
