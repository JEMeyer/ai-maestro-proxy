using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIMaestroProxy.Services;
using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("{*path}")] // Matches all paths
    public partial class ProxyController(
        GpuManagerService gpuManagerService,
        IOptions<PathCategories> pathCategories,
        ProxiedRequestService proxiedRequestService,
        DataService dataService,
        ILogger<ProxyController> logger
    ) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Get, path ?? string.Empty);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Post, path ?? string.Empty);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Delete, path ?? string.Empty);
        }

        // Private helper to centralize request handling
        [NonAction]
        private async Task<IActionResult> HandleRequest(HttpMethod method, string path)
        {
            logger.LogInformation("Handling request: {Method} {Path}", method, path);

            // Read request body if applicable
            string? body = null;
            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                using var reader = new StreamReader(HttpContext.Request.Body);
                body = await reader.ReadToEndAsync();
            }

            try
            {
                ModelAssignment? modelAssignment = null;

                if (pathCategories.Value.GpuBoundPaths.Contains(path))
                {
                    // GPU-bound paths
                    var modelLookupKey = RequestModelParser.GetModelLookupKey(body);
                    modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(modelLookupKey, HttpContext.RequestAborted);

                    if (modelAssignment == null)
                    {
                        return NotFound("No available model assignment found.");
                    }
                }
                else
                {
                    // Non-GPU-bound paths
                    var containerInfos = await dataService.GetContainerInfosAsync(PathFamily.Ollama);
                    if (!containerInfos.Any())
                    {
                        return NotFound("No available containers found.");
                    }

                    var selectedContainer = containerInfos.First();
                    modelAssignment = new ModelAssignment
                    {
                        Name = selectedContainer.ModelName,
                        Ip = selectedContainer.Ip,
                        Port = selectedContainer.Port,
                        GpuIds = string.Empty,
                    };
                }

                // Route request to proxied service
                if (modelAssignment != null)
                {
                    await proxiedRequestService.RouteRequestAsync(HttpContext, modelAssignment, method, body);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling request");
                return StatusCode(500, "Internal server error");
            }

            return new EmptyResult();
        }
    }
}
