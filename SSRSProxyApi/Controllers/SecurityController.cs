using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSRSProxyApi.Models;
using SSRSProxyApi.Services;

namespace SSRSProxyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : ControllerBase
    {
        private readonly ISSRSService _ssrsService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(ISSRSService ssrsService, ILogger<SecurityController> logger)
        {
            _ssrsService = ssrsService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("policies")]
        public async Task<ActionResult<IEnumerable<PolicyInfo>>> GetPolicies([FromQuery] string itemPath)
        {
            try
            {
                var policies = await _ssrsService.GetPoliciesAsync(itemPath);
                return Ok(policies);
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error getting policies for item: {ItemPath}", itemPath);
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize]
        [HttpPost("policies")]
        public async Task<ActionResult> SetPolicies([FromQuery] string itemPath, [FromBody] IEnumerable<PolicyInfo> policies)
        {
            try
            {
                await _ssrsService.SetPoliciesAsync(itemPath, policies);
                return Ok(new { message = "Policies updated successfully" });
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error setting policies for item: {ItemPath}", itemPath);
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize]
        [HttpGet("roles")]
        public async Task<ActionResult<IEnumerable<RoleInfo>>> ListRoles()
        {
            try
            {
                var roles = await _ssrsService.ListRolesAsync();
                return Ok(roles);
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error listing roles");
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize]
        [HttpGet("policies/user")]
        public async Task<ActionResult> GetPoliciesForUserOrGroup([FromQuery] string userOrGroup)
        {
            try
            {
                // Recursively browse all folders and reports starting from root
                var allItems = new List<(string Path, string Type)>();
                async Task BrowseRecursive(string path)
                {
                    var content = await _ssrsService.BrowseFolderAsync(path);
                    foreach (var folder in content.Folders)
                    {
                        allItems.Add((folder.Path, "Folder"));
                        await BrowseRecursive(folder.Path);
                    }
                    foreach (var report in content.Reports)
                    {
                        allItems.Add((report.Path, "Report"));
                    }
                }
                await BrowseRecursive("/");

                var result = new List<object>();
                foreach (var item in allItems)
                {
                    try
                    {
                        var policies = await _ssrsService.GetPoliciesAsync(item.Path);
                        var match = policies.FirstOrDefault(p => string.Equals(p.GroupUserName, userOrGroup, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            result.Add(new {
                                ItemPath = item.Path,
                                ItemType = item.Type,
                                Roles = match.Roles
                            });
                        }
                    }
                    catch (SSRSException ex)
                    {
                        _logger.LogWarning(ex, "Error getting policies for item: {ItemPath}", item.Path);
                    }
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting policies for user/group: {UserOrGroup}", userOrGroup);
                return StatusCode(500, new { message = "Error getting policies for user/group", error = ex.Message });
            }
        }
    }
}
