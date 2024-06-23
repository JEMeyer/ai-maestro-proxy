using Microsoft.AspNetCore.Mvc;

namespace ai_maestro_proxy.Models
{
    public class RequestQueueItem(HttpContext context, string body, bool shouldCheckStream, CancellationToken cancellationToken)
    {
        public HttpContext Context { get; } = context;
        public string Body { get; } = body;
        public bool ShouldCheckStream { get; } = shouldCheckStream;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public TaskCompletionSource<IActionResult> CompletionSource { get; } = new TaskCompletionSource<IActionResult>();
    }
}
