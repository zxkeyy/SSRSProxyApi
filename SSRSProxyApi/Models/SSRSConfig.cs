namespace SSRSProxyApi.Models
{
    public class SSRSConfig
    {
        public string ReportServerUrl { get; set; } = string.Empty;
        public SoapEndpoints SoapEndpoints { get; set; } = new();
        public AuthenticationConfig Authentication { get; set; } = new();
        public int Timeout { get; set; } = 300;
    }

    public class SoapEndpoints
    {
        public string ReportService { get; set; } = string.Empty;
        public string ReportExecution { get; set; } = string.Empty;
    }

    public class AuthenticationConfig
    {
        public string Type { get; set; } = "Windows";
        public string Domain { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}