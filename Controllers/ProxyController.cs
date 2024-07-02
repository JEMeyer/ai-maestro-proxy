using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIMaestroProxy.Services;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    public class ProxyController(GpuManagerService gpuManagerService, IOptions<PathCategories> pathCategories, ProxiedRequestService proxiedRequestService, DataService dataService) : ControllerBase
    {
        [HttpGet("")]
        public IActionResult HealthCheck()
        {
            var content = "Ollama is running";
            Response.Headers["Content-Length"] = content.Length.ToString();
            return Ok(content);
        }

        [HttpGet("{**path}")]
        public async Task<IActionResult> Get([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Get, path);
        }

        [HttpPost("{**path}")]
        public async Task<IActionResult> Post([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Post, path);
        }
        [HttpPut("{**path}")]
        public async Task<IActionResult> Put([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Put, path);
        }

        [HttpDelete("{**path}")]
        public async Task<IActionResult> Delete([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Delete, path);
        }

        [HttpHead("{**path}")]
        public async Task<IActionResult> Head([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [HttpOptions("{**path}")]
        public async Task<IActionResult> Options([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [HttpPatch("{**path}")]
        public async Task<IActionResult> Patch([FromRoute] string path)
        {
            return await HandleRequest(HttpMethod.Head, path);
        }

        [NonAction]
        private async Task<IActionResult> HandleRequest(HttpMethod method, string path)
        {
            var context = HttpContext;
            var body = method != HttpMethod.Get && method != HttpMethod.Head ? await new StreamReader(context.Request.Body).ReadToEndAsync() : null;

            ModelAssignment? modelAssignment = null;
            try
            {
                if (pathCategories.Value.GpuBoundPaths.Contains(path))
                {
                    var modelLookupKey = RequestModelParser.GetModelLookupKey(body);
                    modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(modelLookupKey, context.RequestAborted);

                    if (modelAssignment == null)
                    {
                        return NotFound("No available model assignment found.");
                    }
                }
                else
                {
                    var pathFamily = pathCategories.Value.GetPathFamily(path);
                    IEnumerable<ContainerInfo> allContainerInfos = await dataService.GetContainerInfosAsync(pathFamily);

                    // Looping is weird/early exit, so check it first
                    if (pathCategories.Value.LoopingServerPaths.Contains(path))
                    {
                        await proxiedRequestService.RouteLoopingRequestAsync(context, path, allContainerInfos);
                        return new EmptyResult();
                    }

                    // Otherwise pick a container. check the body for a name/model
                    var requiredModel = RequestModelParser.GetModelLookupKey(body);
                    var containerInfos = allContainerInfos
                        .Where(item => string.IsNullOrEmpty(requiredModel) ||
                                       item.ModelName == requiredModel);

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

            return new EmptyResult();
        }
    }
}
