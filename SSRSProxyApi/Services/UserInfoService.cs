using Microsoft.Extensions.Options;
using SSRSProxyApi.Models;
using System.Security.Principal;

namespace SSRSProxyApi.Services
{
    public interface IUserInfoService
    {
        UserInfo GetCurrentUserInfo();
    }

    public class UserInfoService : IUserInfoService
    {
        private readonly SSRSConfig _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserInfoService> _logger;

        public UserInfoService(
            IOptions<SSRSConfig> config,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserInfoService> logger)
        {
            _config = config.Value;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public UserInfo GetCurrentUserInfo()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var identity = user?.Identity;
            var windowsIdentity = identity as WindowsIdentity;

            // Demo mode: Use service account info from configuration
            if (_config.IsDemo && !string.IsNullOrEmpty(_config.Authentication.Username))
            {
                var demoUserName = string.IsNullOrEmpty(_config.Authentication.Domain)
                    ? _config.Authentication.Username
                    : $"{_config.Authentication.Domain}\\{_config.Authentication.Username}";

                _logger.LogDebug("Demo mode enabled - using service account: {UserName}", demoUserName);

                return new UserInfo
                {
                    IsAuthenticated = true,
                    Name = demoUserName,
                    AuthenticationType = "ServiceAccount (Demo)",
                    IsWindowsIdentity = true,
                    ImpersonationLevel = "Delegation",
                    TokenType = "ServiceAccount",
                    IsSystem = false,
                    IsGuest = false,
                    IsAnonymous = false,
                    Groups = new[] { "BUILTIN\\Users", "NT AUTHORITY\\Authenticated Users" },
                    Claims = new[]
                    {
                        new ClaimInfo { Type = "name", Value = demoUserName },
                        new ClaimInfo { Type = "authenticationmethod", Value = "ServiceAccount" }
                    },
                    CanDelegate = true,
                    IsDemo = true
                };
            }

            // Check if we have a real authenticated user
            if (identity?.IsAuthenticated == true && !string.IsNullOrEmpty(identity.Name))
            {
                _logger.LogDebug("Using authenticated user: {UserName}", identity.Name);

                return new UserInfo
                {
                    IsAuthenticated = true,
                    Name = identity.Name,
                    AuthenticationType = identity.AuthenticationType ?? "Unknown",
                    IsWindowsIdentity = windowsIdentity != null,
                    ImpersonationLevel = windowsIdentity?.ImpersonationLevel.ToString() ?? "N/A",
                    TokenType = windowsIdentity != null ? (windowsIdentity.Token != IntPtr.Zero ? "TokenPresent" : "NoToken") : "N/A",
                    IsSystem = windowsIdentity?.IsSystem ?? false,
                    IsGuest = windowsIdentity?.IsGuest ?? false,
                    IsAnonymous = windowsIdentity?.IsAnonymous ?? false,
                    Groups = windowsIdentity?.Groups?.Select(g => g.Value).ToArray() ?? Array.Empty<string>(),
                    Claims = user?.Claims?.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToArray() ?? Array.Empty<ClaimInfo>(),
                    CanDelegate = windowsIdentity?.ImpersonationLevel == TokenImpersonationLevel.Delegation,
                    IsDemo = false
                };
            }

            // Fallback for when no authentication is configured
            _logger.LogDebug("No authentication configured, using anonymous user");

            return new UserInfo
            {
                IsAuthenticated = false,
                Name = "Anonymous",
                AuthenticationType = "None",
                IsWindowsIdentity = false,
                ImpersonationLevel = "None",
                TokenType = "None",
                IsSystem = false,
                IsGuest = true,
                IsAnonymous = true,
                Groups = Array.Empty<string>(),
                Claims = Array.Empty<ClaimInfo>(),
                CanDelegate = false,
                IsDemo = true
            };
        }
    }

    public class UserInfo
    {
        public bool IsAuthenticated { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AuthenticationType { get; set; } = string.Empty;
        public bool IsWindowsIdentity { get; set; }
        public string ImpersonationLevel { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsGuest { get; set; }
        public bool IsAnonymous { get; set; }
        public string[] Groups { get; set; } = Array.Empty<string>();
        public ClaimInfo[] Claims { get; set; } = Array.Empty<ClaimInfo>();
        public bool CanDelegate { get; set; }
        public bool IsDemo { get; set; }
        public string ServerName { get; set; } = Environment.MachineName;
        public string DomainName { get; set; } = Environment.UserDomainName;
        public DateTime ServerTime { get; set; } = DateTime.Now;
        public string ServerTimeZone { get; set; } = TimeZoneInfo.Local.Id;
    }

    public class ClaimInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}