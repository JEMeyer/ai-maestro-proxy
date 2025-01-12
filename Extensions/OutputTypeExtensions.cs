using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Extensions
{
    public static class OutputTypeExtensions
    {
        public static string ToFriendlyString(this OutputType OutputType)
        {
            return OutputType switch
            {
                OutputType.Images => "images",
                OutputType.Speech => "speech",
                OutputType.Text => "llms",
                _ => throw new ArgumentException("Invalid path family.")
            };
        }
    }
}
