using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIMaestroProxy.Models
{
    public static class RequestModelParser
    {
        public static async Task<RequestModel> ParseFromContext(HttpContext context)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Check if the content type is application/json
            if (context.Request.ContentType?.StartsWith("application/json") ?? false)
            {
                var request = await context.Request.ReadFromJsonAsync<RequestModel>(options) ?? throw new ArgumentException("Invalid request.");
                return request;
            }

            // Handle text/plain content type
            if (context.Request.ContentType?.StartsWith("text/plain") ?? false)
            {
                using var reader = new StreamReader(context.Request.Body);
                var jsonString = await reader.ReadToEndAsync();

                try
                {
                    var request = JsonSerializer.Deserialize<RequestModel>(jsonString, options) ?? throw new ArgumentException("Invalid request.");
                    return request;
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Unable to deserialize JSON: {ex.Message}", ex);
                }
            }

            // If the content type is not supported
            throw new ArgumentException($"Invalid request content type '{context.Request.ContentType}'. Expected 'application/json' or 'text/plain'.");
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
