using System.Text.Json;

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
            var json = JsonDocument.Parse(body ?? "{}");
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

            // We don't have :latest in the database, so if that is present, just rip it off
            if (lookupKey.EndsWith(":latest"))
                lookupKey = lookupKey[..^7];

            return lookupKey;
        }

        public static string SetKeepAlive(string? body, int keepAlive = -1)
        {
            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(body ?? "{}");

            // Modify the dictionary
            if (jsonObject != null)
            {
                jsonObject["keep_alive"] = keepAlive;
            }

            // Serialize the dictionary back to a JSON string
            return JsonSerializer.Serialize(jsonObject);
        }
    }
}
