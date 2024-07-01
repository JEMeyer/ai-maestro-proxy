using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class SpeechHandler(DataService dataService, ComputeHandler computeHandler, ProxiedRequestService proxiedRequestService)
    {
        public async Task HandleSpeechComputeRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);
            ArgumentNullException.ThrowIfNull(request.Model);

            await computeHandler.HandleComputeRequestAsync(context, request.Model, request);
        }

        // Just call any server that has our model, we shouldn't need GPUs for this so not reserving
        public async Task HandleSpeechProcessRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);
            ArgumentNullException.ThrowIfNull(request.Name);

            // Try to get an available model assignment
            var modelAssignment = (await dataService.GetModelAssignmentsAsync(request.Name)).First();
            ArgumentNullException.ThrowIfNull(modelAssignment);

            await proxiedRequestService.RouteRequestAsync(context, modelAssignment, request);
        }
    }
}
