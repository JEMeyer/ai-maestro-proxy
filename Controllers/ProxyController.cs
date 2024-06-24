using Microsoft.AspNetCore.Mvc;
using ai_maestro_proxy.Models;
using ai_maestro_proxy.Services;
using System.Text;
using Serilog;
using ai_maestro_proxy.ModelBinders;
using Newtonsoft.Json;

namespace ai_maestro_proxy.Controllers
{
    [ApiController]
    [Route("")]
    public class ProxyController(RequestHandlerService requestHandlerService) : ControllerBase
    {
        [HttpPost("txt2img")]
        public async Task<IActionResult> HandleTxt2Img(
            [ModelBinder(typeof(RequestModelBinder))] RequestModel request,
            CancellationToken cancellationToken)
        {
            using (var reader = new StreamReader(Request.Body))
            {
                var body = await reader.ReadToEndAsync(cancellationToken);
                Log.Warning("Request body: {Body}", body);
            }
            var requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);
            Log.Information("Endpoint hit: {Endpoint}, Model requested: {Model}, RequestModel: {RequestModel}", "txt2img", request.Model, requestJson);
            return await requestHandlerService.HandleRequest("txt2img", request, HttpContext, cancellationToken);
        }

        [HttpPost("img2img")]
        public async Task<IActionResult> HandleImgImg([ModelBinder(BinderType = typeof(RequestModelBinder))] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("img2img", request, HttpContext, cancellationToken);
        }

        [HttpPost("api/generate")]
        public async Task<IActionResult> Handlegenerate([ModelBinder(BinderType = typeof(RequestModelBinder))] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/generate", request, HttpContext, cancellationToken);
        }

        [HttpPost("api/chat")]
        public async Task<IActionResult> HandleChat([ModelBinder(BinderType = typeof(RequestModelBinder))] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/chat", request, HttpContext, cancellationToken);
        }

        [HttpPost("api/embeddings")]
        public async Task<IActionResult> HandleEmbeddings([ModelBinder(BinderType = typeof(RequestModelBinder))] RequestModel request, CancellationToken cancellationToken)
        {
            return await requestHandlerService.HandleRequest("api/embeddings", request, HttpContext, cancellationToken);
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
