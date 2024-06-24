using ai_maestro_proxy.Services;

namespace ai_maestro_proxy.Models
{
    public class GpuLock(GpuManagerService gpuManagerService, Assignment assignment) : IDisposable
    {
        private readonly GpuManagerService _gpuManagerService = gpuManagerService ?? throw new ArgumentNullException(nameof(gpuManagerService));
        private readonly Assignment _assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
        private bool _disposed = false;

        public Assignment Assignment => _assignment;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _gpuManagerService.UnlockGpus(_assignment.GpuIds.Split(','));
                }
                _disposed = true;
            }
        }
    }
}
