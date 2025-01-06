using AIMaestroProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIMaestroProxy.Controllers
{
    [ApiController]
    [Route("gpu")]
    public class GpuController(ILogger<GpuController> logger, GpuManagerService gpuManagerService) : ControllerBase
    {
        [HttpPost("reserve/{modelName}")]
        public async Task<IActionResult> ReserveGpu(string modelName)
        {
            var context = HttpContext;
            try
            {
                var modelAssignment = await gpuManagerService.GetAvailableModelAssignmentAsync(modelName, context.RequestAborted);
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

        [HttpPost("release/{gpuIds}")]
        public IActionResult ReleaseGpu(string gpuIds)
        {
            try
            {
                gpuManagerService.UnlockGPUs(gpuIds.Split(','));
                return new JsonResult(new { Message = "GPU released successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error releasing gpus");
                return BadRequest(new { Message = "Failed to release GPU(s)" });
            }
        }

        [HttpPost("ping/{gpuIds}")]
        public IActionResult PingGpu(string gpuIds)
        {
            try
            {
                gpuManagerService.RefreshGpuActivity(gpuIds.Split(','));
                return new JsonResult(new { Message = "Ping successful" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling ping request");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
