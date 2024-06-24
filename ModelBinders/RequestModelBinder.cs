using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Serilog;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.ModelBinders
{
    public class RequestModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            if (valueProviderResult == ValueProviderResult.None)
            {
                Log.Warning("No value found in the value provider for {ModelName}", bindingContext.ModelName);
                return Task.CompletedTask;
            }

            var values = valueProviderResult.FirstValue;

            if (string.IsNullOrEmpty(values))
            {
                Log.Warning("Value is null or empty for {ModelName}", bindingContext.ModelName);
                return Task.CompletedTask;
            }

            // Initialize the model with a temporary value for the required property
            var model = new RequestModel
            {
                Model = string.Empty
            };

            try
            {
                Log.Information("Parsing JSON for {ModelName}", bindingContext.ModelName);
                var jObject = JObject.Parse(values);
                model.Model = jObject["model"]?.ToString() ?? string.Empty;
                model.Stream = jObject["stream"]?.ToObject<bool?>();

                foreach (var property in jObject)
                {
                    if (property.Key != "model" && property.Key != "stream" && property.Value != null)
                    {
                        model.AdditionalProperties[property.Key] = property.Value;
                        Log.Information("Added property {Key} with value {Value}", property.Key, property.Value);
                    }
                }

                bindingContext.Result = ModelBindingResult.Success(model);
                Log.Information("Successfully bound model for {ModelName}", bindingContext.ModelName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error binding model for {ModelName}", bindingContext.ModelName);
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
