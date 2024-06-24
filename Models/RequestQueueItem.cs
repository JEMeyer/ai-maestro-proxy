using Microsoft.AspNetCore.Mvc;

namespace ai_maestro_proxy.Models
{
    public class RequestQueueItem(HttpContext context, RequestModel model, CancellationToken cancellationToken)
    {
        public HttpContext Context { get; } = context;
        public RequestModel Model { get; } = model;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public TaskCompletionSource<IActionResult> CompletionSource { get; } = new TaskCompletionSource<IActionResult>();
    }
}
