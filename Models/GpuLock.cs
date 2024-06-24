using ai_maestro_proxy.Services;

namespace ai_maestro_proxy.Models
{
    public class GpuLock(GpuManagerService gpuManagerService, RequestHandlerService requestHandlerService, Assignment assignment) : IDisposable
    {
        private readonly GpuManagerService gpuManagerService = gpuManagerService ?? throw new ArgumentNullException(nameof(gpuManagerService));
        private readonly RequestHandlerService requestHandlerService = requestHandlerService ?? throw new ArgumentNullException(nameof(requestHandlerService));
        private readonly Assignment assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
        private bool disposed = false;

        public Assignment Assignment => assignment;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    gpuManagerService.UnlockGpus(assignment.GpuIds.Split(','));
                    requestHandlerService.ProcessQueue();
                }
                disposed = true;
            }
        }
    }
}
