using AIMaestroProxy.Interfaces;
using AIMaestroProxy.Models;
using AIMaestroProxy.Services;
using Microsoft.AspNetCore.Mvc;
using Sentry;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GpuController(
        ILogger<GpuController> logger,
        IGpuManagerService gpuManagerService,
        DataService dataService,
        IHub sentryHub) : ControllerBase
    {

        /// <summary>
        /// /gpus/text/reserve/qwq or /gpus/images/reserve/sdxl-turbo
        /// </summary>
        [HttpPost("reserve")]
        public async Task<IActionResult> ReserveGpu([FromBody] GpuReservationRequest request)
        {
            var childSpan = sentryHub.GetSpan()?.StartChild("reserve-gpu");
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(request.ModelName) || string.IsNullOrWhiteSpace(request.OutputType))
            {
                return BadRequest("ModelName and OutputType are required.");
            }

            var context = HttpContext;
            try
            {
                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(GetOutputTypeFromString(request.OutputType), request.ModelName, context.RequestAborted);
                if (modelAssignment == null)
                {
                    return NotFound("No available model assignment found.");
                }
                childSpan?.Finish(SpanStatus.Ok);
                return new JsonResult(modelAssignment);
            }
            catch (Exception ex)
            {
                childSpan?.Finish(ex);
                logger.LogError(ex, "Error processing /reserve/modelName request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("release")]
        public IActionResult ReleaseGpu([FromBody] GpuStatusRequest request)
        {
            var childSpan = sentryHub.GetSpan()?.StartChild("release-gpu");
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }
            if (request.GpuIds == null || request.GpuIds.Length == 0)
            {
                return BadRequest("GpuIds must be provided.");
            }

            try
            {
                gpuManagerService.UnlockGPUs(request.GpuIds);
                childSpan?.Finish(SpanStatus.Ok);
                return new JsonResult(new { Message = "GPU released successfully" });
            }
            catch (Exception ex)
            {
                childSpan?.Finish(ex);
                logger.LogError(ex, "Error releasing gpus");
                return BadRequest(new { Message = "Failed to release GPU(s)" });
            }
        }

        [HttpPost("ping")]
        public IActionResult PingGpu([FromBody] GpuStatusRequest request)
        {
            var childSpan = sentryHub.GetSpan()?.StartChild("ping-gpu");
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }
            if (request.GpuIds == null || request.GpuIds.Length == 0)
            {
                return BadRequest("GpuIds must be provided.");
            }

            try
            {
                gpuManagerService.RefreshGpuActivity(request.GpuIds);
                childSpan?.Finish(SpanStatus.Ok);
                return new JsonResult(new { Message = "Ping successful" });
            }
            catch (Exception ex)
            {
                childSpan?.Finish(ex);
                logger.LogError(ex, "Error handling ping request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("cache")]
        public IActionResult ClearCache()
        {
            var childSpan = sentryHub.GetSpan()?.StartChild("clear-cache");
            try
            {
                var removedCount = dataService.ClearCache();
                logger.LogInformation("Cleared {Count} GPU status entries from cache", removedCount);
                childSpan?.Finish(SpanStatus.Ok);
                return Ok(new { removed = removedCount });
            }
            catch (Exception ex)
            {
                childSpan?.Finish(ex);
                logger.LogError(ex, "Error clearing GPU status cache");
                return StatusCode(500, "Failed to clear cache");
            }
        }
    }
}
