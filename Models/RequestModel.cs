using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIMaestroProxy.Models
{
    public static class RequestModelParser
    {
        public static async Task<RequestModel> ParseFromContext(HttpContext context)
        {
            var request = await context.Request.ReadFromJsonAsync<RequestModel>() ?? throw new ArgumentException("Invalid request.");
            return request;
        }
    }

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


