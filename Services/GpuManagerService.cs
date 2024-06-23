using Serilog;
using System.Collections.Concurrent;

namespace ai_maestro_proxy.Services
{
    public class GpuManagerService
    {
        private static readonly ConcurrentDictionary<string, bool> lockedGpus = new();

        public bool TryLockGpus(string[] gpuIds)
        {
            foreach (string gpuId in gpuIds)
            {
                if (lockedGpus.TryGetValue(gpuId, out bool isLocked) && isLocked)
                {
                    return false;
                }
            }

            foreach (string gpuId in gpuIds)
            {
                lockedGpus[gpuId] = true;
                Log.Information("GPU {GpuId} marked as taken", gpuId);
            }
            return true;
        }

        public void UnlockGpus(string[] gpuIds)
        {
            foreach (string gpuId in gpuIds)
            {
                lockedGpus[gpuId] = false;
                Log.Information("GPU {GpuId} released", gpuId);
            }
        }
    }
}
