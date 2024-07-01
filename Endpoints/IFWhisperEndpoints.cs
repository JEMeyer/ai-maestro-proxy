using AIMaestroProxy.Handlers;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Endpoints
{
    public static class IFWhisperEndpoints
    {
        private static readonly string _defaultIFWhisperModel = "openai/whisper-large-v3";
        public static void MapIFWhisperEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/transcribe", async (HttpContext context, ComputeHandler handlerService) =>
            {
                var request = await RequestModelParser.ParseFromContext(context);
                var modelLookupKey = request.Model;

                await handlerService.HandleComputeRequestAsync(context, modelLookupKey ?? _defaultIFWhisperModel, request);
            });

            endpoints.MapPost("/transcribe/stream", async (HttpContext context, ComputeHandler handlerService) =>
            {
                var request = await RequestModelParser.ParseFromContext(context);
                var modelLookupKey = request.Model;

                await handlerService.HandleComputeRequestAsync(context, modelLookupKey ?? _defaultIFWhisperModel, request);
            });
        }
    }
}
