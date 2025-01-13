namespace AIMaestroProxy.Models
{
    public static class PathCategories
    {
        /// <summary>
        /// The different types of servers/endpoints
        /// </summary>
        public enum OutputType
        {
            Text,
            Speech,
            Images,
            Unknown
        }

        /// <summary>
        /// Used when we can use any instance so we know which type of container to request.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static OutputType GetOutputTypeFromPath(string path)
        {
            if (path == "" || path.StartsWith("api/")) return OutputType.Text;
            if (path.StartsWith("audio/")) return OutputType.Speech;
            if (path.StartsWith("txt2img") || path.StartsWith("img2img")) return OutputType.Images;

            return OutputType.Unknown;
        }

        public static OutputType GetOutputTypeFromString(string sType)
        {
            return sType switch
            {
                "text" => OutputType.Text,
                "speech" => OutputType.Speech,
                "images" => OutputType.Images,
                _ => OutputType.Unknown,
            };
        }

        /// <summary>
        /// Used to get nice looking strings to use for redis key and db
        /// </summary>
        /// <param name="OutputType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToStorageName(this OutputType OutputType)
        {
            return OutputType switch
            {
                OutputType.Images => "diffusors",
                OutputType.Speech => "speech_models",
                OutputType.Text => "llms",
                _ => throw new ArgumentException("Invalid path family.")
            };
        }
    }
}
