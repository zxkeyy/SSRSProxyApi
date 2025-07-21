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
}