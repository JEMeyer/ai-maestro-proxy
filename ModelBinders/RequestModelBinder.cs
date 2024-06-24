using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Serilog;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.ModelBinders
{
    public class RequestModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            // Read the request body
            using var reader = new StreamReader(bindingContext.HttpContext.Request.Body);
            var body = await reader.ReadToEndAsync();

            // Parse the JSON
            var jsonObject = JObject.Parse(body);
            var modelString = jsonObject["model"]?.ToString();

            ArgumentNullException.ThrowIfNull(modelString);

            // Create the RequestModel
            var requestModel = new RequestModel
            {
                Model = modelString,
                Stream = jsonObject["stream"]?.ToObject<bool>(),
                AdditionalProperties = []
            };

            // Add all properties except 'model' and 'stream' to AdditionalProperties
            foreach (var prop in jsonObject.Properties())
            {
                if (prop.Name != "model" && prop.Name != "stream")
                {
                    requestModel.AdditionalProperties[prop.Name] = prop.Value;
                }
            }

            // Set the result
            bindingContext.Result = ModelBindingResult.Success(requestModel);
        }
    }
}
