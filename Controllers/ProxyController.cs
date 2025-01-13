using Microsoft.AspNetCore.Mvc;
using AIMaestroProxy.Services;
using AIMaestroProxy.Models;
using System.Text;
using System.Text.RegularExpressions;
using static AIMaestroProxy.Models.PathCategories;
using AIMaestroProxy.Interfaces;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("{*path}")] // Route all paths
    public partial class ProxyController(
        IGpuManagerService gpuManagerService,
        DataService dataService,
        ProxiedRequestService proxiedRequestService,
        ILogger<ProxyController> logger
    ) : ControllerBase
    {
        [GeneratedRegex("model\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex ModelRegex();


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

        [HttpPut]
        public async Task<IActionResult> Put([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Put, path ?? string.Empty);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete([FromRoute] string? path)
        {
            return await HandleRequest(HttpMethod.Delete, path ?? string.Empty);
        }

        // In ProxyController.cs

        [HttpDelete("/cache")]
        public IActionResult ClearCache()
        {
            try
            {
                var removedCount = dataService.ClearCache();
                logger.LogInformation("Cleared {Count} GPU status entries from cache", removedCount);
                return Ok(new { removed = removedCount });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error clearing GPU status cache");
                return StatusCode(500, "Failed to clear cache");
            }
        }

        // Centralized request handling
        [NonAction]
        private async Task<IActionResult> HandleRequest(HttpMethod method, string path)
        {
            logger.LogInformation("Handling request: {Method} {Path}", method, path);

            // Initialize variables
            ModelAssignment? modelAssignment = null;
            string? modelName = null;
            string? bodyContent = null;

            try
            {
                // If it's a POST request, read the body content first
                if (HttpContext.Request.Method == HttpMethod.Post.Method)
                {
                    using var reader = new StreamReader(HttpContext.Request.Body);
                    bodyContent = await reader.ReadToEndAsync();

                    // If this path requires GPU, extract model name from the body we just read
                    if (GpuPaths.ComputeRequired.Contains(path))
                    {
                        var match = ModelRegex().Match(bodyContent);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            modelName = match.Groups[1].Value;
                        }

                        if (string.IsNullOrEmpty(modelName))
                        {
                            logger.LogError("Model name not found in the request body.");
                            return BadRequest("Model name is required.");
                        }
                        else if (modelName.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
                        {
                            // Remove ":latest" from the end of the model name - maestrodb doesn't use it
                            modelName = modelName[..^7];
                        }
                    }
                }

                // Determine if the path requires GPU resources
                if (GpuPaths.ComputeRequired.Contains(path))
                {
                    logger.LogDebug("GPU-bound request for path: {Path}", path);

                    // We already have modelName from the body if this was a POST
                    if (string.IsNullOrEmpty(modelName))
                    {
                        logger.LogError("Model name not found in the request.");
                        return BadRequest("Model name is required.");
                    }

                    // Assign a GPU container based on the model
                    modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(GetOutputTypeFromPath(path), modelName, HttpContext.RequestAborted);
                    if (modelAssignment == null)
                    {
                        logger.LogError("No available GPU assignment for model: {ModelName}", modelName);
                        return NotFound("No available model assignment found.");
                    }
                }
                else
                {
                    // Non-GPU-bound paths handling (e.g., listing models, version info)
                    logger.LogDebug("Handling non-GPU-bound path: {Path}", path);
                    var modelAssignments = await dataService.GetModelAssignmentsAsync(GetOutputTypeFromPath(path));
                    if (!modelAssignments.Any())
                    {
                        return NotFound("No available containers found.");
                    }

                    var selectedContainer = modelAssignments.First();
                    modelAssignment = new ModelAssignment
                    {
                        Name = selectedContainer.Name,
                        Ip = selectedContainer.Ip,
                        Port = selectedContainer.Port,
                        GpuIds = string.Empty,
                    };
                }

                // If we do have GPU IDs, start a keep-alive loop
                CancellationTokenSource? refreshCts = null;
                Task? refreshTask = null;

                if (!string.IsNullOrEmpty(modelAssignment.GpuIds))
                {
                    refreshCts = new CancellationTokenSource();
                    refreshTask = gpuManagerService.KeepGpuRefreshAliveAsync(modelAssignment.GpuIds.Split(","), refreshCts.Token);
                }

                try
                {
                    // Route the actual request to the proxied service
                    await proxiedRequestService.RouteRequestAsync(HttpContext, modelAssignment, method, bodyContent);
                }
                finally
                {
                    // Stop refreshing once this request is done
                    if (refreshCts != null)
                    {
                        refreshCts.Cancel();
                        try
                        {
                            await (refreshTask ?? Task.CompletedTask);
                        }
                        catch (TaskCanceledException)
                        {
                            // normal upon cancellation
                        }
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                logger.LogWarning(oce, "Request was canceled by the client: {Path}", path);
                if (!HttpContext.Response.HasStarted)
                {
                    HttpContext.Response.StatusCode = 499; // Client Closed Request
                }
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while handling request: {Path}", path);
                if (!HttpContext.Response.HasStarted)
                {
                    return StatusCode(500, "Internal server error.");
                }
                return new EmptyResult();
            }
            finally
            {
                // Ensure GPU resources are released
                if (modelAssignment != null && !string.IsNullOrEmpty(modelAssignment.GpuIds))
                {
                    gpuManagerService.UnlockGPUs(modelAssignment.GpuIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries));
                    logger.LogDebug("Unlocked GPUs for model: {ModelName}", modelName);
                }
            }

            // The response is already handled by RouteRequestAsync
            return new EmptyResult();
        }
    }
}
