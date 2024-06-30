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
        private string? Name { get; set; }

        private string? Model { get; set; }

        [JsonIgnore]
        public string ModelName
        {
            get => Name ?? Model ?? "";
        }

        [JsonPropertyName("keep_alive")]
        public int? KeepAlive { get; set; }

        public bool? Stream { get; set; }

        public Dictionary<string, JsonElement> AdditionalData { get; set; } = [];
    }
}


