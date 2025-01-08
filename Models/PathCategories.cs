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
            if (path.StartsWith("diffusion/")) return OutputType.Images;

            return OutputType.Unknown;
        }

        public static OutputType GetOutputTypeFromString(string sType)
        {
            switch (sType)
            {
                case "textstring":
                    return OutputType.Text;
                case "speechstring":
                    return OutputType.Speech;
                case "imagesstring":
                    return OutputType.Images;
                default:
                    return OutputType.Unknown;
            }
        }

    }
}
