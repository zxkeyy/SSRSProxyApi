using SSRSProxyApi.Models;

namespace SSRSProxyApi.Services
{
    public interface ISSRSService
    {
        Task<IEnumerable<ReportInfo>> GetReportsAsync(string folderPath = "/");
        Task<IEnumerable<ReportParameter>> GetReportParametersAsync(string reportPath);
        Task<byte[]> RenderReportAsync(string reportPath, Dictionary<string, object> parameters);
    }
}