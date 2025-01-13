using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Interfaces
{
    public interface IGpuManagerService : IDisposable
    {
        /// <summary>
        /// Gets an available model assignment based on output type and model name. Any returned ModelAssignments have the gpu reserved for you.
        /// </summary>
        Task<ModelAssignment?> GetAvailableModelAssignmentAsync(OutputType outputType, string modelName, CancellationToken cancellationToken);

        /// <summary>
        /// Unlocks specified GPUs, making them available for other tasks
        /// </summary>
        void UnlockGPUs(string[] gpuIds);

        /// <summary>
        /// Updates the last activity timestamp for specified GPUs
        /// </summary>
        void RefreshGpuActivity(string[] gpuIds);

        /// <summary>
        /// Checks if the GPU management service is functioning correctly
        /// </summary>
        bool IsHealthy();
    }

    public record GpuStatus
    {
        /// <summary>
        /// Unique identifier for the GPU
        /// </summary>
        public required string GpuId { get; init; }

        /// <summary>
        /// Name of the model currently running on the GPU, if any
        /// </summary>
        public string? CurrentModel { get; init; }

        /// <summary>
        /// Timestamp of the last recorded activity
        /// </summary>
        public DateTime? LastActivity { get; init; }

        /// <summary>
        /// Indicates whether the GPU is currently in use
        /// </summary>
        public bool IsLocked => !string.IsNullOrEmpty(CurrentModel);
    }
}
