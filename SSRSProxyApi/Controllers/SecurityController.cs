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
    }
}
