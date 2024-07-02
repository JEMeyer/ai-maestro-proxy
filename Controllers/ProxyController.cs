using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIMaestroProxy.Services;
using AIMaestroProxy.Models;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    public class ProxyController(GpuManagerService gpuManagerService,
                               IOptions<PathCategories> pathCategories,
                               ProxiedRequestService proxiedRequestService,
                               DataService dataService,
                               ILogger<ProxyController> logger) : ControllerBase
    {

        [HttpGet("")]
        public IActionResult HealthCheck()
        {
            var content = "Ollama is running";
            Response.Headers["Content-Length"] = content.Length.ToString();
            return Ok(content);
        }

        [HttpGet("{*path}")]
        public async Task<IActionResult> Get([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Get, path);
        }

        [HttpPost("{*path}")]
        public async Task<IActionResult> Post([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Post, path);
        }
        [HttpPut("{*path}")]
        public async Task<IActionResult> Put([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Put, path);
        }

        [HttpDelete("{*path}")]
        public async Task<IActionResult> Delete([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Delete, path);
        }

        [HttpHead("{*path}")]
        public async Task<IActionResult> Head([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [HttpOptions("{*path}")]
        public async Task<IActionResult> Options([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [HttpPatch("{*path}")]
        public async Task<IActionResult> Patch([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [NonAction]
        private async Task<IActionResult> HandleRequest(HttpMethod method, string path)
        {
            path = "/" + path;

            logger.LogDebug("Starting HandleRequest");
            var context = HttpContext;
            var body = method != HttpMethod.Get && method != HttpMethod.Head ? await new StreamReader(context.Request.Body).ReadToEndAsync() : null;
            try
            {

                ModelAssignment? modelAssignment = null;
                try
                {
                    if (pathCategories.Value.GpuBoundPaths.Contains(path))
                    {
                        logger.LogDebug("GPU Bound");
                        var modelLookupKey = RequestModelParser.GetModelLookupKey(body);
                        modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(modelLookupKey, context.RequestAborted);

                        if (modelAssignment == null)
                        {
                            return NotFound("No available model assignment found.");
                        }
                    }
                    else
                    {
                        var pathFamily = pathCategories.Value.GetNonComputePathFamily(path);
                        logger.LogDebug("Determined family was {family}", pathFamily switch
                        {
                            PathFamily.Ollama => "Ollama",
                            PathFamily.Coqui => "Coqui",
                            PathFamily.Diffusion => "Diffusion",
                            _ => "Unknown"
                        });
                        IEnumerable<ContainerInfo> allContainerInfos = await dataService.GetContainerInfosAsync(pathFamily);

                        // Looping is weird/early exit, so check it first
                        if (pathCategories.Value.LoopingServerPaths.Contains(path))
                        {
                            logger.LogDebug("Looping request");
                            await proxiedRequestService.RouteLoopingRequestAsync(context, path, allContainerInfos);
                            return new EmptyResult();
                        }

                        // Otherwise pick a container. check the body for a name/model
                        var requiredModel = RequestModelParser.GetModelLookupKey(body);
                        var containerInfos = allContainerInfos
                            .Where(item => string.IsNullOrEmpty(requiredModel) ||
                                           item.ModelName == requiredModel);

                        logger.LogDebug("Handling non-compute, found {count} container candidates", containerInfos.Count());

                        modelAssignment = new ModelAssignment
                        {
                            Name = string.Empty, // Doesn't matter
                            Ip = containerInfos.First().Ip,
                            Port = containerInfos.First().Port,
                            GpuIds = string.Empty // Doesn't matter
                        };
                    }
                    if (modelAssignment != null)
                    {
                        await proxiedRequestService.RouteRequestAsync(context, modelAssignment, method, body);
                    }
                }
                finally
                {
                    if (modelAssignment != null && pathCategories.Value.GpuBoundPaths.Contains(path))
                    {
                        gpuManagerService.UnlockGPUs(modelAssignment.GpuIds.Split(","));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error getting available model assignment: {ex}", ex.ToString());
                return StatusCode(500, "Internal server error.");
            }

            return new EmptyResult();
        }
    }
}
