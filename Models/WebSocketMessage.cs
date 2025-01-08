// Models/WebSocketMessage.cs
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Models
{
    public class WebSocketMessage
    {
        public required string Command { get; set; } // e.g., "reserve", "release"
        public string? ModelName { get; set; } // Used with "reserve"
        public string? OutputType { get; set; } // Used with "reserve"
        public string[]? GpuIds { get; set; } // Used with "release"
    }

    public class WebSocketResponse
    {
        public required string Status { get; set; } // "success" or "error"
        public string? Message { get; set; } // Optional message
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string[]? GpuIds { get; set; }

    }
}
