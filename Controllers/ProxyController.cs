using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIMaestroProxy.Services;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    public partial class ProxyController(GpuManagerService gpuManagerService,
                               IOptions<PathCategories> pathCategories,
                               ProxiedRequestService proxiedRequestService,
                               DataService dataService,
                               ILogger<ProxyController> logger) : ControllerBase
    {
        [HttpGet("{*path}")]
        public async Task<IActionResult> Get([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Get, path ?? string.Empty);
        }


        [HttpHead("{*path}")]
        public async Task<IActionResult> Head([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Head, path ?? string.Empty);
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
    }
}
