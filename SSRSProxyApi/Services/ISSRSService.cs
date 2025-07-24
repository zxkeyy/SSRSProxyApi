using SSRSProxyApi.Models;

namespace SSRSProxyApi.Services
{
    public interface ISSRSService
    {
        Task<IEnumerable<ReportInfo>> GetReportsAsync(string folderPath = "/");
        Task<FolderContent> BrowseFolderAsync(string folderPath = "/");
        Task<IEnumerable<ReportParameter>> GetReportParametersAsync(string reportPath);
        Task<byte[]> RenderReportAsync(string reportPath, Dictionary<string, object> parameters, string format = "PDF");
        Task<IEnumerable<PolicyInfo>> GetPoliciesAsync(string itemPath);
        Task SetPoliciesAsync(string itemPath, IEnumerable<PolicyInfo> policies);
        Task<IEnumerable<RoleInfo>> ListRolesAsync();
        Task CreateFolderAsync(string parentPath, string folderName, string description = "");
        Task DeleteFolderAsync(string folderPath);
        Task CreateReportAsync(string parentPath, string reportName, byte[] definition, string description = "");
        Task DeleteReportAsync(string reportPath);
        Task MoveItemAsync(string itemPath, string newParentPath, string newName);
    }
}