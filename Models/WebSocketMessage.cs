using System.Text.Json.Serialization;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Models
{
    public class WebSocketMessage
    {
        [JsonPropertyName("command")]
        public required string Command { get; set; } // e.g., "reserve", "release"

        [JsonPropertyName("modelName")]
        public string? ModelName { get; set; } // Used with "reserve"

        [JsonPropertyName("outputType")]
        public OutputType? OutputType { get; set; } // Used with "reserve"

        [JsonPropertyName("gpuIds")]
        public string[]? GpuIds { get; set; } // Used with "release"
    }

    public class WebSocketResponse
    {
        [JsonPropertyName("status")]
        public required string Status { get; set; } // "success" or "error"

        [JsonPropertyName("message")]
        public string? Message { get; set; } // Optional message

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("port")]
        public int? Port { get; set; }

        [JsonPropertyName("gpuIds")]
        public string[]? GpuIds { get; set; }

    }
}
