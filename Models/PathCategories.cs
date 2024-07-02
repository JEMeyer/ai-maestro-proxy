namespace AIMaestroProxy.Models
{
    public class PathCategories
    {
        /// <summary>
        /// Standard 'compute' requests that actually run the AI
        /// </summary>
        public required List<string> GpuBoundPaths { get; set; }
        /// <summary>
        /// Requests that, to be 'accurate', should loop over all servers of a given type.
        /// </summary>
        public required List<string> LoopingServerPaths { get; set; }

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
        /// Used when we can use any instance or the looping instances so we know which type of container to request.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public PathFamily GetPathFamily(string path)
        {
            if (path == "/" || path.Contains("/api/") || path.Contains("/v1/chat/completions")) return PathFamily.Ollama;
            if (path.Contains("/languages") || path.Contains("/studio_speakers") || path.Contains("/tts") || path.Contains("/stt")) return PathFamily.Coqui;
            if (path.Contains("/upload") || path.Contains("/img2img") || path.Contains("/txt2img")) return PathFamily.Diffusion;

            return PathFamily.Unknown;
        }
    }
}
