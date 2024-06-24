using ai_maestro_proxy.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Serilog;

namespace ai_maestro_proxy.ModelBinders
{
    public class RequestModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Metadata.ModelType == typeof(RequestModel))
            {
                Log.Information("Providing RequestModelBinder for {ModelType}", context.Metadata.ModelType);
                return new BinderTypeModelBinder(typeof(RequestModelBinder));
            }

            Log.Information("No binder available for {ModelType}", context.Metadata.ModelType);
            return null;
        }
    }
}
