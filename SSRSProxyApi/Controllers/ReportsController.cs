using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSRSProxyApi.Models;
using SSRSProxyApi.Services;
using SSRSProxyApi.Attributes;
using System.Security.Principal;

namespace SSRSProxyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly ISSRSService _ssrsService;
        private readonly IUserInfoService _userInfoService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ISSRSService ssrsService, IUserInfoService userInfoService, ILogger<ReportsController> logger)
        {
            _ssrsService = ssrsService;
            _userInfoService = userInfoService;
            _logger = logger;
        }

        /// <summary>
        /// Test SSRS connectivity and basic functionality
        /// </summary>
        /// <returns>SSRS connection status and available reports</returns>
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("test-connection")]
        public async Task<ActionResult> TestSSRSConnection()
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
            try
            {
                _logger.LogInformation("User '{User}' testing SSRS connection (Demo: {IsDemo})", currentUser, userInfo.IsDemo);
                
                var folderContent = await _ssrsService.BrowseFolderAsync("/");
                
                return Ok(new 
                { 
                    message = "SSRS connection successful", 
                    user = currentUser,
                    isDemo = userInfo.IsDemo,
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
                    isDemo = userInfo.IsDemo,
                    timestamp = DateTime.Now 
                });
            }
        }

        /// <summary>
        /// Browse folder structure with both folders and reports
        /// </summary>
        /// <param name="folderPath">Folder path to browse (default: root "/")</param>
        /// <returns>Folder contents with folders and reports</returns>
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("browse")]
        public async Task<ActionResult<FolderContent>> BrowseFolder([FromQuery] string folderPath = "/")
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
            try
            {
                _logger.LogInformation("User '{User}' browsing folder: {FolderPath} (Demo: {IsDemo})", currentUser, folderPath, userInfo.IsDemo);
                
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
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReportInfo>>> GetReports([FromQuery] string folderPath = "/")
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
            try
            {
                _logger.LogInformation("User '{User}' retrieving reports from folder: {FolderPath} (Demo: {IsDemo})", currentUser, folderPath, userInfo.IsDemo);
                
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
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("parameters")]
        public async Task<ActionResult<IEnumerable<ReportParameter>>> GetReportParameters([FromQuery] string reportPath)
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
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

                _logger.LogInformation("User '{User}' retrieving parameters for report: {ReportPath} (Demo: {IsDemo})", currentUser, reportPath, userInfo.IsDemo);
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
        [Authorize(Policy = "ConditionalAuth")]
        [HttpPost("render")]
        public async Task<ActionResult> RenderReport([FromBody] RenderRequest request)
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
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

                _logger.LogInformation("User '{User}' rendering report: {ReportPath} with {ParameterCount} parameters (Demo: {IsDemo})", 
                    currentUser, request.ReportPath, request.Parameters.Count, userInfo.IsDemo);

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
        [Authorize(Policy = "ConditionalAuth")]
        [HttpPost("render/{format}")]
        public async Task<ActionResult> RenderReportInFormat(string format, [FromBody] RenderRequest request)
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
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

                _logger.LogInformation("User '{User}' rendering report: {ReportPath} in format: {Format} with {ParameterCount} parameters (Demo: {IsDemo})", 
                    currentUser, request.ReportPath, upperFormat, request.Parameters.Count, userInfo.IsDemo);

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
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("user")]
        public ActionResult GetCurrentUser()
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            
            _logger.LogInformation("Current user info requested: {UserName} (Demo: {IsDemo})", userInfo.Name, userInfo.IsDemo);
            return Ok(userInfo);
        }

        /// <summary>
        /// Search for reports and folders by name or description (recursive)
        /// </summary>
        /// <param name="query">Search term (case-insensitive, matches name or description)</param>
        /// <returns>List of matching reports and folders</returns>
        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] string query)
        {
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var currentUser = userInfo.Name ?? "Unknown";
            
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { message = "Query parameter is required" });

            try
            {
                _logger.LogInformation("User '{User}' searching for items with query: {Query} (Demo: {IsDemo})", currentUser, query, userInfo.IsDemo);
                var results = new List<object>();
                async Task BrowseRecursive(string path)
                {
                    var content = await _ssrsService.BrowseFolderAsync(path);
                    foreach (var folder in content.Folders)
                    {
                        if (folder.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(folder.Description) && folder.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        {
                            results.Add(new {
                                Type = "Folder",
                                folder.Name,
                                folder.Path,
                                folder.Description,
                                folder.CreatedDate,
                                folder.ModifiedDate
                            });
                        }
                        await BrowseRecursive(folder.Path);
                    }
                    foreach (var report in content.Reports)
                    {
                        if (report.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(report.Description) && report.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        {
                            results.Add(new {
                                Type = "Report",
                                report.Name,
                                report.Path,
                                report.Description,
                                report.CreatedDate,
                                report.ModifiedDate
                            });
                        }
                    }
                }
                await BrowseRecursive("/");
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for items with query: {Query}", query);
                return StatusCode(500, new { message = "Error searching for items", error = ex.Message });
            }
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