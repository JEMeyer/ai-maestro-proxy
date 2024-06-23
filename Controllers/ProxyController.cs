using Microsoft.AspNetCore.Mvc;
using ai_maestro_proxy.Models;
using ai_maestro_proxy.Services;
using System.Text;
using Serilog;

namespace ai_maestro_proxy.Controllers
{
    [ApiController]
    [Route("")]
    public class ProxyController(RequestHandlerService requestHandlerService) : ControllerBase
    {
        [HttpPost("txt2img")]
        public async Task<IActionResult> HandleTxt2Img([FromBody] RequestModel request, CancellationToken cancellationToken)
        {
            Log.Information("Endpoint hit: {Endpoint}, Model requested: {Model}", "txt2img", request.Model);
            return await requestHandlerService.HandleRequest("txt2img", request, false, await ReadRequestBodyAsync(), HttpContext, cancellationToken);
        }

        [HttpPost("img2img")]
        public async Task<IActionResult> HandleImgImg([FromBody] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("img2img", request, false, await ReadRequestBodyAsync(), HttpContext, cancellationToken);
        }

        [HttpPost("api/generate")]
        public async Task<IActionResult> Handlegenerate([FromBody] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/generate", request, true, await ReadRequestBodyAsync(), HttpContext, cancellationToken);
        }

        [HttpPost("api/chat")]
        public async Task<IActionResult> HandleChat([FromBody] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/chat", request, true, await ReadRequestBodyAsync(), HttpContext, cancellationToken);
        }

        [HttpPost("api/embeddings")]
        public async Task<IActionResult> HandleEmbeddings([FromBody] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/embeddings", request, true, await ReadRequestBodyAsync(), HttpContext, cancellationToken);
        }

        private async Task<string> ReadRequestBodyAsync()
        {
            HttpContext.Request.EnableBuffering();
            using var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            HttpContext.Request.Body.Position = 0;
            return body;
        }

    }
}
