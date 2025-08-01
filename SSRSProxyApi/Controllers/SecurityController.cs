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

        [Authorize(Policy = "ConditionalAuth")]
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

        [Authorize(Policy = "ConditionalAuth")]
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

        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("roles/system")]
        public async Task<ActionResult<IEnumerable<RoleInfo>>> ListSystemRoles()
        {
            try
            {
                var roles = await _ssrsService.ListSystemRolesAsync();
                return Ok(roles);
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error listing system roles");
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("roles/catalog")]
        public async Task<ActionResult<IEnumerable<RoleInfo>>> ListCatalogRoles()
        {
            try
            {
                var roles = await _ssrsService.ListCatalogRolesAsync();
                return Ok(roles);
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error listing catalog roles");
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize(Policy = "ConditionalAuth")]
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

        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("policies/user")]
        public async Task<ActionResult<IEnumerable<object>>> GetUserPolicies([FromQuery] string userOrGroup)
        {
            if (string.IsNullOrEmpty(userOrGroup))
            {
                return BadRequest(new { message = "userOrGroup parameter is required" });
            }

            try
            {
                var results = new List<object>();
                
                // This is a simplified implementation - you might want to implement a more efficient search
                async Task CheckPoliciesRecursive(string path)
                {
                    try
                    {
                        var policies = await _ssrsService.GetPoliciesAsync(path);
                        var userPolicies = policies.Where(p => 
                            p.GroupUserName.Equals(userOrGroup, StringComparison.OrdinalIgnoreCase));
                        
                        if (userPolicies.Any())
                        {
                            // Determine item type by trying to browse it
                            string itemType = "Unknown";
                            try
                            {
                                var content = await _ssrsService.BrowseFolderAsync(path);
                                itemType = "Folder";
                            }
                            catch
                            {
                                itemType = "Report"; // Assume it's a report if we can't browse it
                            }
                            
                            results.Add(new
                            {
                                itemPath = path,
                                itemType = itemType,
                                roles = userPolicies.SelectMany(p => p.Roles).Distinct().ToArray()
                            });
                        }
                        
                        // Recursively check subfolders (only if this is a folder)
                        try
                        {
                            var content = await _ssrsService.BrowseFolderAsync(path);
                            foreach (var folder in content.Folders)
                            {
                                await CheckPoliciesRecursive(folder.Path);
                            }
                            foreach (var report in content.Reports)
                            {
                                await CheckPoliciesRecursive(report.Path);
                            }
                        }
                        catch
                        {
                            // Not a folder, skip recursion
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not check policies for path {Path}: {Error}", path, ex.Message);
                    }
                }
                
                await CheckPoliciesRecursive("/");
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user policies for: {UserOrGroup}", userOrGroup);
                return StatusCode(500, new { message = "Error retrieving user policies", error = ex.Message });
            }
        }

        [Authorize(Policy = "ConditionalAuth")]
        [HttpGet("policies/system")]
        public async Task<ActionResult<IEnumerable<PolicyInfo>>> GetSystemPolicies()
        {
            try
            {
                var policies = await _ssrsService.GetSystemPoliciesAsync();
                return Ok(policies);
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error getting system policies");
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }

        [Authorize(Policy = "ConditionalAuth")]
        [HttpPost("policies/system")]
        public async Task<ActionResult> SetSystemPolicies([FromBody] IEnumerable<PolicyInfo> policies)
        {
            try
            {
                await _ssrsService.SetSystemPoliciesAsync(policies);
                return Ok(new { message = "System policies updated successfully" });
            }
            catch (SSRSException ex)
            {
                _logger.LogError(ex, "Error setting system policies");
                return StatusCode(ex.HttpStatusCode, new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        }
    }
}
