using System.Text.Json;
using System.Text.Json.Serialization;

namespace ai_maestro_proxy.Models
{
    public class RequestModel
    {
        public required string Model { get; set; }

        [JsonPropertyName("keep_alive")]
        public int? KeepAlive { get; set; }

        public bool? Stream { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalData { get; set; } = [];
    }
}


