using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SSRSProxyApi.Models;

namespace SSRSProxyApi.Attributes
{
    /// <summary>
    /// Custom authorization attribute that bypasses authorization when IsDemo is true
    /// </summary>
    public class ConditionalAuthorizeAttribute : Attribute, IAuthorizationRequirement
    {
    }

    public class ConditionalAuthorizationHandler : AuthorizationHandler<ConditionalAuthorizeAttribute>
    {
        private readonly SSRSConfig _config;

        public ConditionalAuthorizationHandler(IOptions<SSRSConfig> config)
        {
            _config = config.Value;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ConditionalAuthorizeAttribute requirement)
        {
            // If demo mode is enabled, always succeed authorization
            if (_config.IsDemo)
            {
                context.Succeed(requirement);
            }
            // If demo mode is disabled, check if user is authenticated
            else if (context.User.Identity?.IsAuthenticated == true)
            {
                context.Succeed(requirement);
            }
            // Otherwise, fail authorization (this will trigger authentication challenge)

            return Task.CompletedTask;
        }
    }
}