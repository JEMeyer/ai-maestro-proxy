using AIMaestroProxy.Models;
using AIMaestroProxy.Services;

namespace AIMaestroProxy.Handlers
{
    public class DiffusionHandler(DataService dataService, ComputeHandler computeHandler, ProxiedRequestService proxiedRequestService)
    {
        public async Task HandleDiffusionComputeRequestAsync(HttpContext context)
        {
            var request = await RequestModelParser.ParseFromContext(context);
            ArgumentNullException.ThrowIfNull(request.Model);

            await computeHandler.HandleComputeRequestAsync(context, request.Model, request);
        }

        // Just call any server that has our model, we shouldn't need GPUs for this so not reserving
        public async Task HandleDiffusionProcessRequestAsync(HttpContext context)
        {
            // Doesn't matter which instance we upload to.
            var containerInfos = await dataService.GetLlmContainerInfosAsync();
            ModelAssignment? modelAssignment = null;
            if (containerInfos.Any())
            {
                modelAssignment = new ModelAssignment
                {
                    Name = "Random diffusion model name", // Doesn't matter
                    Ip = containerInfos.First().Ip,
                    Port = containerInfos.First().Port,
                    GpuIds = "-1" // Doesn't matter
                };
            }
            ArgumentNullException.ThrowIfNull(modelAssignment);

            await proxiedRequestService.RouteRequestAsync(context, modelAssignment);
        }
    }
}
