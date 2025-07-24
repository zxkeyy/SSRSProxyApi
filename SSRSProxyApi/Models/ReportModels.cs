namespace SSRSProxyApi.Models
{
    public class RenderRequest
    {
        public string ReportPath { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ReportInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class ReportParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Nullable { get; set; }
        public bool AllowBlank { get; set; }
        public bool MultiValue { get; set; }
        public string[] ValidValues { get; set; } = Array.Empty<string>();
        public string DefaultValue { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }

    public class FolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class FolderContent
    {
        public string CurrentPath { get; set; } = string.Empty;
        public List<FolderInfo> Folders { get; set; } = new();
        public List<ReportInfo> Reports { get; set; } = new();
    }

    /// <summary>
    /// Custom exception for SSRS-specific errors that can be mapped to appropriate HTTP status codes
    /// </summary>
    public class SSRSException : Exception
    {
        public int HttpStatusCode { get; }
        public string ErrorCode { get; }

        public SSRSException(int httpStatusCode, string errorCode, string message) : base(message)
        {
            HttpStatusCode = httpStatusCode;
            ErrorCode = errorCode;
        }

        public SSRSException(int httpStatusCode, string errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            HttpStatusCode = httpStatusCode;
            ErrorCode = errorCode;
        }
    }

    public class PolicyInfo
    {
        public string GroupUserName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public class RoleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}