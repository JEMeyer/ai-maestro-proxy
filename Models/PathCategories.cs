namespace AIMaestroProxy.Models
{
    public class PathCategories
    {
        /// <summary>
        /// Standard 'compute' requests that actually run the AI
        /// </summary>
        public required List<string> GpuBoundPaths { get; set; }

        /// <summary>
        /// Whisper not listed due to laziness and also doens't need it since it's all compute
        /// </summary>
        public enum PathFamily
        {
            Ollama,
            Coqui,
            Diffusion,
            Unknown
        }

        /// <summary>
        /// Used when we can use any instance so we know which type of container to request.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public PathFamily GetNonComputePathFamily(string path)
        {
            if (path == "" || path.StartsWith("api/")) return PathFamily.Ollama;
            if (path.StartsWith("languages") || path.StartsWith("studio_speakers")) return PathFamily.Coqui;
            if (path.StartsWith("upload")) return PathFamily.Diffusion;

            return PathFamily.Unknown;
        }
    }
}
