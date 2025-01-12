using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Interfaces
{
    public interface IGpuManagerService : IDisposable
    {
        // Core GPU management
        Task<ModelAssignment?> GetAvailableModelAssignmentAsync(OutputType outputType, string? modelName, CancellationToken cancellationToken);
        void UnlockGPUs(string[] gpuIds);
        void RefreshGpuActivity(string[] gpuIds);

        // Monitoring/health
        Task<bool> IsHealthyAsync();
        IEnumerable<GpuStatus> GetCurrentGpuStatus();
    }

    public record GpuStatus
    {
        public string GpuId { get; init; } = string.Empty;
        public string? CurrentModel { get; init; }
        public DateTime? LastActivity { get; init; }
        public bool IsLocked => !string.IsNullOrEmpty(CurrentModel);
    }
}
