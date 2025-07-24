using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSRSProxyApi.Services;

namespace SSRSProxyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManagementController : ControllerBase
    {
        private readonly ISSRSService _ssrsService;
        private readonly ILogger<ManagementController> _logger;

        public ManagementController(ISSRSService ssrsService, ILogger<ManagementController> logger)
        {
            _ssrsService = ssrsService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("folder")]
        public async Task<IActionResult> CreateFolder([FromQuery] string parentPath, [FromQuery] string folderName, [FromQuery] string description = "")
        {
            await _ssrsService.CreateFolderAsync(parentPath, folderName, description);
            return Ok(new { message = "Folder created successfully" });
        }

        [Authorize]
        [HttpDelete("folder")]
        public async Task<IActionResult> DeleteFolder([FromQuery] string folderPath)
        {
            await _ssrsService.DeleteFolderAsync(folderPath);
            return Ok(new { message = "Folder deleted successfully" });
        }

        [Authorize]
        [HttpPost("report")]
        public async Task<IActionResult> CreateReport([FromQuery] string parentPath, [FromQuery] string reportName, [FromBody] byte[] definition, [FromQuery] string description = "")
        {
            await _ssrsService.CreateReportAsync(parentPath, reportName, definition, description);
            return Ok(new { message = "Report created successfully" });
        }

        [Authorize]
        [HttpDelete("report")]
        public async Task<IActionResult> DeleteReport([FromQuery] string reportPath)
        {
            await _ssrsService.DeleteReportAsync(reportPath);
            return Ok(new { message = "Report deleted successfully" });
        }

        [Authorize]
        [HttpPost("move")]
        public async Task<IActionResult> MoveItem([FromQuery] string itemPath, [FromQuery] string targetPath)
        {
            await _ssrsService.MoveItemAsync(itemPath, targetPath);
            return Ok(new { message = "Item moved successfully" });
        }
    }
}
