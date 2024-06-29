using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIMaestroProxy.Models
{
    public class RequestModel
    {
        public string? Name { get; set; }

        public string? Model { get; set; }

        [JsonPropertyName("keep_alive")]
        public int? KeepAlive { get; set; }

        public bool? Stream { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalData { get; set; } = [];
    }
}


