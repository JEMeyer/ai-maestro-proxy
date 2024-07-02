using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIMaestroProxy.Models
{
    public static class RequestModelParser
    {
        /// <summary>
        /// Pulls name or model off the body. Will trim :latest since we dont use that
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public static string GetModelLookupKey(string? body)
        {
            body ??= string.Empty;

            var json = JsonDocument.Parse(body);
            string? lookupKey = null;
            if (json.RootElement.TryGetProperty("name", out var name))
            {
                lookupKey = name.GetString();
            }
            if (json.RootElement.TryGetProperty("model", out var model))
            {
                lookupKey = model.GetString();
            }
            lookupKey ??= string.Empty;

            if (lookupKey.EndsWith(":latest"))
                lookupKey = lookupKey[..^7];

            return lookupKey;
        }

        public static async Task<RequestModel> ParseFromContext(HttpContext context)
        {
            try
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
            }
            catch
            {
                return new RequestModel();
            }

            // If the content type is not supported just return empty body and let the downstream deal with it
            return new RequestModel();
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
