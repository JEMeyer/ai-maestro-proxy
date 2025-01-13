using AIMaestroProxy.Interfaces;
using AIMaestroProxy.Models;
using AIMaestroProxy.Services;
using Microsoft.AspNetCore.Mvc;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GpuController(ILogger<GpuController> logger, IGpuManagerService gpuManagerService, DataService dataService) : ControllerBase
    {

        /// <summary>
        /// /gpus/text/reserve/qwq or /gpus/images/reserve/sdxl-turbo
        /// </summary>
        [HttpPost("reserve")]
        public async Task<IActionResult> ReserveGpu([FromBody] GpuReservationRequest request)
        {
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
                return new JsonResult(modelAssignment);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing /reserve/modelName request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("release")]
        public IActionResult ReleaseGpu([FromBody] GpuStatusRequest request)
        {
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
                return new JsonResult(new { Message = "GPU released successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error releasing gpus");
                return BadRequest(new { Message = "Failed to release GPU(s)" });
            }
        }

        [HttpPost("ping")]
        public IActionResult PingGpu([FromBody] GpuStatusRequest request)
        {
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
                return new JsonResult(new { Message = "Ping successful" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling ping request");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("cache")]
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
    }
}
