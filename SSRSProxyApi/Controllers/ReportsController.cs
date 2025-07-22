using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSRSProxyApi.Models;
using SSRSProxyApi.Services;
using System.Security.Principal;

namespace SSRSProxyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly ISSRSService _ssrsService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ISSRSService ssrsService, ILogger<ReportsController> logger)
        {
            _ssrsService = ssrsService;
            _logger = logger;
        }

        /// <summary>
        /// Get all reports under the root folder (/) - Test endpoint without auth
        /// </summary>
        /// <returns>List of reports</returns>
        [AllowAnonymous]
        [HttpGet("test")]
        public async Task<ActionResult<IEnumerable<ReportInfo>>> GetReportsTest()
        {
            try
            {
                _logger.LogInformation("Testing SSRS connection - retrieving reports from root folder");
                var reports = await _ssrsService.GetReportsAsync("/");
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports");
                return StatusCode(500, new { message = "Error retrieving reports", error = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Get all reports under the root folder (/)
        /// </summary>
        /// <returns>List of reports</returns>
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReportInfo>>> GetReports()
        {
            try
            {
                _logger.LogInformation("Retrieving reports from root folder");
                var reports = await _ssrsService.GetReportsAsync("/");
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports");
                return StatusCode(500, new { message = "Error retrieving reports", error = ex.Message });
            }
        }

        /// <summary>
        /// Get available parameters for a specific report
        /// </summary>
        /// <param name="reportPath">Report path (can include folders) - pass as query parameter</param>
        /// <returns>List of report parameters</returns>
        [Authorize]
        [HttpGet("parameters")]
        public async Task<ActionResult<IEnumerable<ReportParameter>>> GetReportParameters([FromQuery] string reportPath)
        {
            try
            {
                if (string.IsNullOrEmpty(reportPath))
                {
                    return BadRequest(new { message = "Report path is required as query parameter" });
                }

                // Ensure path starts with /
                if (!reportPath.StartsWith("/"))
                {
                    reportPath = "/" + reportPath;
                }

                _logger.LogInformation("Retrieving parameters for report: {ReportPath}", reportPath);
                var parameters = await _ssrsService.GetReportParametersAsync(reportPath);
                return Ok(parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving parameters for report: {ReportPath}", reportPath);
                return StatusCode(500, new { message = "Error retrieving report parameters", error = ex.Message });
            }
        }

        /// <summary>
        /// Render a report to PDF
        /// </summary>
        /// <param name="request">Report path and parameters</param>
        /// <returns>PDF file</returns>
        [Authorize]
        [HttpPost("render")]
        public async Task<ActionResult> RenderReport([FromBody] RenderRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ReportPath))
                {
                    return BadRequest(new { message = "Report path is required" });
                }

                // Ensure path starts with /
                if (!request.ReportPath.StartsWith("/"))
                {
                    request.ReportPath = "/" + request.ReportPath;
                }

                _logger.LogInformation("Rendering report: {ReportPath} with {ParameterCount} parameters", 
                    request.ReportPath, request.Parameters.Count);

                var pdfBytes = await _ssrsService.RenderReportAsync(request.ReportPath, request.Parameters);
                
                var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering report: {ReportPath}", request.ReportPath);
                return StatusCode(500, new { message = "Error rendering report", error = ex.Message });
            }
        }

        /// <summary>
        /// Get current user information for debugging authentication
        /// </summary>
        /// <returns>Current user details</returns>
        [Authorize]
        [HttpGet("user")]
        public ActionResult GetCurrentUser()
        {
            var user = HttpContext.User;
            var identity = user?.Identity;
            
            var userInfo = new
            {
                IsAuthenticated = identity?.IsAuthenticated ?? false,
                Name = identity?.Name ?? "Unknown",
                AuthenticationType = identity?.AuthenticationType ?? "None",
                IsWindowsIdentity = OperatingSystem.IsWindows() && identity is WindowsIdentity,
                Claims = user?.Claims.Select(c => new { c.Type, c.Value }).ToList()
            };

            _logger.LogInformation("Current user info: {@UserInfo}", userInfo);
            return Ok(userInfo);
        }
    }
}