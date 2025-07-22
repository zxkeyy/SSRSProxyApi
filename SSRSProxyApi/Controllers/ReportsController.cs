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
        /// Test SSRS connectivity and basic functionality
        /// </summary>
        /// <returns>SSRS connection status and available reports</returns>
        [Authorize]
        [HttpGet("test-connection")]
        public async Task<ActionResult> TestSSRSConnection()
        {
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
            try
            {
                _logger.LogInformation("User '{User}' testing SSRS connection", currentUser);
                
                var folderContent = await _ssrsService.BrowseFolderAsync("/");
                
                return Ok(new 
                { 
                    message = "SSRS connection successful", 
                    user = currentUser,
                    reportCount = folderContent.Reports.Count,
                    folderCount = folderContent.Folders.Count,
                    reports = folderContent.Reports.Select(r => new { r.Name, r.Path }).Take(5),
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSRS connection test failed for user '{User}'", currentUser);
                return StatusCode(500, new { 
                    message = "SSRS connection failed", 
                    error = ex.Message, 
                    user = currentUser,
                    timestamp = DateTime.Now 
                });
            }
        }

        /// <summary>
        /// Browse folder structure with both folders and reports
        /// </summary>
        /// <param name="folderPath">Folder path to browse (default: root "/")</param>
        /// <returns>Folder contents with folders and reports</returns>
        [Authorize]
        [HttpGet("browse")]
        public async Task<ActionResult<FolderContent>> BrowseFolder([FromQuery] string folderPath = "/")
        {
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
            try
            {
                _logger.LogInformation("User '{User}' browsing folder: {FolderPath}", currentUser, folderPath);
                
                var folderContent = await _ssrsService.BrowseFolderAsync(folderPath);
                return Ok(folderContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing folder for user '{User}': {FolderPath}", currentUser, folderPath);
                return StatusCode(500, new { message = "Error browsing folder", error = ex.Message, user = currentUser });
            }
        }

        /// <summary>
        /// Get reports in a specific folder (legacy endpoint)
        /// </summary>
        /// <param name="folderPath">Folder path (default: root "/")</param>
        /// <returns>List of reports</returns>
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReportInfo>>> GetReports([FromQuery] string folderPath = "/")
        {
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
            try
            {
                _logger.LogInformation("User '{User}' retrieving reports from folder: {FolderPath}", currentUser, folderPath);
                
                var reports = await _ssrsService.GetReportsAsync(folderPath);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports for user '{User}': {FolderPath}", currentUser, folderPath);
                return StatusCode(500, new { message = "Error retrieving reports", error = ex.Message, user = currentUser });
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
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
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

                _logger.LogInformation("User '{User}' retrieving parameters for report: {ReportPath}", currentUser, reportPath);
                var parameters = await _ssrsService.GetReportParametersAsync(reportPath);
                return Ok(parameters);
            }
            catch (SSRSException ssrsEx)
            {
                _logger.LogWarning("SSRS error for user '{User}' retrieving parameters for report '{ReportPath}': {ErrorCode} - {Message}", 
                    currentUser, reportPath, ssrsEx.ErrorCode, ssrsEx.Message);
                
                return StatusCode(ssrsEx.HttpStatusCode, new 
                { 
                    message = ssrsEx.Message, 
                    errorCode = ssrsEx.ErrorCode,
                    user = currentUser,
                    reportPath = reportPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving parameters for user '{User}' for report: {ReportPath}", currentUser, reportPath);
                return StatusCode(500, new { message = "An unexpected error occurred while retrieving report parameters", error = ex.Message, user = currentUser });
            }
        }

        /// <summary>
        /// Render a report to PDF (default format)
        /// </summary>
        /// <param name="request">Report path and parameters</param>
        /// <returns>PDF file</returns>
        [Authorize]
        [HttpPost("render")]
        public async Task<ActionResult> RenderReport([FromBody] RenderRequest request)
        {
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
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

                _logger.LogInformation("User '{User}' rendering report: {ReportPath} with {ParameterCount} parameters", 
                    currentUser, request.ReportPath, request.Parameters.Count);

                var pdfBytes = await _ssrsService.RenderReportAsync(request.ReportPath, request.Parameters);
                
                var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (SSRSException ssrsEx)
            {
                _logger.LogWarning("SSRS error for user '{User}' rendering report '{ReportPath}': {ErrorCode} - {Message}", 
                    currentUser, request.ReportPath, ssrsEx.ErrorCode, ssrsEx.Message);
                
                return StatusCode(ssrsEx.HttpStatusCode, new 
                { 
                    message = ssrsEx.Message, 
                    errorCode = ssrsEx.ErrorCode,
                    user = currentUser,
                    reportPath = request.ReportPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error rendering report for user '{User}': {ReportPath}", currentUser, request.ReportPath);
                return StatusCode(500, new { message = "An unexpected error occurred while rendering the report", error = ex.Message, user = currentUser });
            }
        }

        /// <summary>
        /// Render report in specific format (PDF, Excel, Word, CSV, XML)
        /// </summary>
        /// <param name="format">Output format (PDF, EXCEL, WORD, CSV, XML)</param>
        /// <param name="request">Report path and parameters</param>
        /// <returns>Report in specified format</returns>
        [Authorize]
        [HttpPost("render/{format}")]
        public async Task<ActionResult> RenderReportInFormat(string format, [FromBody] RenderRequest request)
        {
            var currentUser = HttpContext.User?.Identity?.Name ?? "Unknown";
            try
            {
                if (string.IsNullOrEmpty(request.ReportPath))
                {
                    return BadRequest(new { message = "Report path is required" });
                }

                // Validate format
                var supportedFormats = new[] { "PDF", "EXCEL", "WORD", "CSV", "XML", "IMAGE" };
                var upperFormat = format.ToUpper();
                if (!supportedFormats.Contains(upperFormat))
                {
                    return BadRequest(new { message = $"Unsupported format. Supported formats: {string.Join(", ", supportedFormats)}" });
                }

                // Ensure path starts with /
                if (!request.ReportPath.StartsWith("/"))
                {
                    request.ReportPath = "/" + request.ReportPath;
                }

                _logger.LogInformation("User '{User}' rendering report: {ReportPath} in format: {Format} with {ParameterCount} parameters", 
                    currentUser, request.ReportPath, upperFormat, request.Parameters.Count);

                var reportBytes = await _ssrsService.RenderReportAsync(request.ReportPath, request.Parameters, upperFormat);
                
                var (mimeType, fileExtension) = GetMimeTypeAndExtension(upperFormat);
                var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension}";
                
                return File(reportBytes, mimeType, fileName);
            }
            catch (SSRSException ssrsEx)
            {
                _logger.LogWarning("SSRS error for user '{User}' rendering report '{ReportPath}' in format '{Format}': {ErrorCode} - {Message}", 
                    currentUser, request.ReportPath, format, ssrsEx.ErrorCode, ssrsEx.Message);
                
                return StatusCode(ssrsEx.HttpStatusCode, new 
                { 
                    message = ssrsEx.Message, 
                    errorCode = ssrsEx.ErrorCode,
                    user = currentUser,
                    reportPath = request.ReportPath,
                    format = format
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error rendering report for user '{User}': {ReportPath} in format: {Format}", currentUser, request.ReportPath, format);
                return StatusCode(500, new { message = "An unexpected error occurred while rendering the report", error = ex.Message, user = currentUser, format });
            }
        }

        /// <summary>
        /// Get current user information
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
                IsWindowsIdentity = OperatingSystem.IsWindows() && identity is WindowsIdentity
            };

            _logger.LogInformation("Current user info requested: {UserName}", userInfo.Name);
            return Ok(userInfo);
        }

        private static (string mimeType, string extension) GetMimeTypeAndExtension(string format)
        {
            return format.ToUpper() switch
            {
                "PDF" => ("application/pdf", "pdf"),
                "EXCEL" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                "WORD" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                "CSV" => ("text/csv", "csv"),
                "XML" => ("application/xml", "xml"),
                "IMAGE" => ("image/png", "png"),
                _ => ("application/octet-stream", "bin")
            };
        }
    }
}