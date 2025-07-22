using Microsoft.Extensions.Options;
using SSRSProxyApi.Models;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Principal;

namespace SSRSProxyApi.Services
{
    public class SSRSService : ISSRSService
    {
        private readonly SSRSConfig _config;
        private readonly ILogger<SSRSService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SSRSService(IOptions<SSRSConfig> config, ILogger<SSRSService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _config = config.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            
            _logger.LogInformation("SSRSService initialized with pass-through authentication for URL: {Url}", 
                _config.SoapEndpoints.ReportService);
        }

        /// <summary>
        /// Creates an HttpClient with the current user's Windows credentials
        /// </summary>
        private HttpClient CreateHttpClientForCurrentUser()
        {
            var handler = new HttpClientHandler()
            {
                UseDefaultCredentials = false,
                PreAuthenticate = true
            };

            var currentUser = _httpContextAccessor.HttpContext?.User;
            var currentUserName = currentUser?.Identity?.Name ?? "Anonymous";

            // Try to get Windows Identity for the current user (Windows platform only)
            if (OperatingSystem.IsWindows() && 
                currentUser?.Identity is WindowsIdentity windowsIdentity && 
                windowsIdentity.IsAuthenticated)
            {
                _logger.LogInformation("Creating HttpClient for authenticated Windows user: {UserName}", currentUserName);
                
                // Use the Windows identity token for authentication
                try
                {
                    // Use default credentials which will be the current user's credentials
                    handler.UseDefaultCredentials = true;
                    _logger.LogDebug("Using default credentials for user: {UserName}", currentUserName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to use Windows identity for user {UserName}, falling back to configured credentials", currentUserName);
                    // Fall back to configured credentials if available
                    SetConfiguredCredentials(handler);
                }
            }
            else if (!string.IsNullOrEmpty(_config.Authentication.Username))
            {
                _logger.LogInformation("No Windows identity available, using configured service account: {Domain}\\{Username}", 
                    _config.Authentication.Domain, _config.Authentication.Username);
                SetConfiguredCredentials(handler);
            }
            else
            {
                _logger.LogInformation("Using default credentials for user: {UserName}", currentUserName);
                handler.UseDefaultCredentials = true;
            }

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SSRSProxyApi/1.0");
            
            return httpClient;
        }

        /// <summary>
        /// Sets configured credentials from appsettings
        /// </summary>
        private void SetConfiguredCredentials(HttpClientHandler handler)
        {
            var credentialCache = new CredentialCache();
            var uri = new Uri(_config.SoapEndpoints.ReportService);
            
            var credential = new NetworkCredential(
                _config.Authentication.Username,
                _config.Authentication.Password,
                _config.Authentication.Domain);
            
            credentialCache.Add(uri, "NTLM", credential);
            credentialCache.Add(uri, "Negotiate", credential);
            
            handler.Credentials = credentialCache;
        }

        public async Task<IEnumerable<ReportInfo>> GetReportsAsync(string folderPath = "/")
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogInformation("User '{User}' attempting to retrieve reports from folder: {FolderPath}", currentUser, folderPath);
                _logger.LogInformation("Using SSRS endpoint: {Endpoint}", _config.SoapEndpoints.ReportService);
                
                var soapEnvelope = CreateListChildrenSoapEnvelope(folderPath);
                _logger.LogDebug("SOAP Request: {SoapEnvelope}", soapEnvelope);
                
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                
                // Add specific SOAP action for ListChildren
                var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
                {
                    Content = content
                };
                request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/ListChildren");
                
                var response = await httpClient.SendAsync(request);
                
                _logger.LogInformation("SSRS Response Status for user '{User}': {StatusCode}", currentUser, response.StatusCode);
                _logger.LogDebug("SSRS Response Headers: {Headers}", response.Headers.ToString());
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("SSRS Error Response Status for user '{User}': {StatusCode}", currentUser, response.StatusCode);
                    _logger.LogError("SSRS Error Response Content: {ErrorContent}", errorContent);
                    
                    // Check for authentication issues specifically
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("Authentication failed for user '{User}'. Check user permissions in SSRS.", currentUser);
                        
                        // Log WWW-Authenticate header if present
                        if (response.Headers.WwwAuthenticate.Any())
                        {
                            var authHeaders = string.Join(", ", response.Headers.WwwAuthenticate.Select(h => h.ToString()));
                            _logger.LogError("WWW-Authenticate headers: {AuthHeaders}", authHeaders);
                        }
                    }
                    
                    throw new HttpRequestException($"SSRS request failed for user '{currentUser}' with status {response.StatusCode}: {errorContent}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("SSRS Response: {Response}", responseContent);
                
                var reports = ParseListChildrenResponse(responseContent);
                _logger.LogInformation("Successfully retrieved {ReportCount} reports for user '{User}' from folder: {FolderPath}", 
                    reports.Count(), currentUser, folderPath);
                
                return reports;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Error retrieving reports for user '{User}' from folder: {FolderPath}", currentUser, folderPath);
                throw;
            }
        }

        public async Task<IEnumerable<ReportParameter>> GetReportParametersAsync(string reportPath)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogInformation("User '{User}' attempting to retrieve parameters for report: {ReportPath}", currentUser, reportPath);
                
                var soapEnvelope = CreateGetReportParametersSoapEnvelope(reportPath);
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                
                // Add specific SOAP action for GetReportParameters
                var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
                {
                    Content = content
                };
                request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/GetReportParameters");
                
                var response = await httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("SSRS Error Response for user '{User}': {ErrorContent}", currentUser, errorContent);
                    throw new HttpRequestException($"SSRS request failed for user '{currentUser}' with status {response.StatusCode}: {errorContent}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("SSRS Parameters Response: {Response}", responseContent);
                
                var parameters = ParseGetReportParametersResponse(responseContent);
                _logger.LogInformation("Successfully retrieved {ParameterCount} parameters for user '{User}' for report: {ReportPath}", 
                    parameters.Count(), currentUser, reportPath);
                
                return parameters;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Error retrieving parameters for user '{User}' for report: {ReportPath}", currentUser, reportPath);
                throw;
            }
        }

        public async Task<byte[]> RenderReportAsync(string reportPath, Dictionary<string, object> parameters)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogInformation("User '{User}' attempting to render report: {ReportPath}", currentUser, reportPath);
                
                // First load the report
                var loadSoapEnvelope = CreateLoadReportSoapEnvelope(reportPath);
                var loadContent = new StringContent(loadSoapEnvelope, Encoding.UTF8, "text/xml");
                
                var loadResponse = await httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, loadContent);
                loadResponse.EnsureSuccessStatusCode();
                
                var loadResponseContent = await loadResponse.Content.ReadAsStringAsync();
                var executionId = ExtractExecutionId(loadResponseContent);
                
                // Set parameters if any
                if (parameters.Any())
                {
                    var setParamsSoapEnvelope = CreateSetExecutionParametersSoapEnvelope(executionId, parameters);
                    var setParamsContent = new StringContent(setParamsSoapEnvelope, Encoding.UTF8, "text/xml");
                    
                    var setParamsResponse = await httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, setParamsContent);
                    setParamsResponse.EnsureSuccessStatusCode();
                }
                
                // Render the report
                var renderSoapEnvelope = CreateRenderSoapEnvelope(executionId);
                var renderContent = new StringContent(renderSoapEnvelope, Encoding.UTF8, "text/xml");
                
                var renderResponse = await httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, renderContent);
                renderResponse.EnsureSuccessStatusCode();
                
                var renderResponseContent = await renderResponse.Content.ReadAsStringAsync();
                var reportBytes = ExtractRenderedReport(renderResponseContent);
                
                _logger.LogInformation("Successfully rendered report for user '{User}': {ReportPath} ({Size} bytes)", 
                    currentUser, reportPath, reportBytes.Length);
                
                return reportBytes;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Error rendering report for user '{User}': {ReportPath}", currentUser, reportPath);
                throw;
            }
        }

        private string CreateListChildrenSoapEnvelope(string folderPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ListChildren xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Item>{folderPath}</Item>
            <Recursive>false</Recursive>
        </ListChildren>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateGetReportParametersSoapEnvelope(string reportPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <GetReportParameters xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Report>{reportPath}</Report>
            <HistoryID></HistoryID>
            <ForRendering>true</ForRendering>
            <Values></Values>
            <Credentials></Credentials>
        </GetReportParameters>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateLoadReportSoapEnvelope(string reportPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <LoadReport xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Report>{reportPath}</Report>
            <HistoryID></HistoryID>
        </LoadReport>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateSetExecutionParametersSoapEnvelope(string executionId, Dictionary<string, object> parameters)
        {
            var paramXml = string.Join("", parameters.Select(p => 
                $"<ParameterValue><Name>{p.Key}</Name><Value>{p.Value}</Value></ParameterValue>"));
            
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <SetExecutionParameters xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Parameters>{paramXml}</Parameters>
            <ParameterLanguage>en-us</ParameterLanguage>
        </SetExecutionParameters>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateRenderSoapEnvelope(string executionId)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <Render xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Format>PDF</Format>
            <DeviceInfo></DeviceInfo>
        </Render>
    </soap:Body>
</soap:Envelope>";
        }

        private IEnumerable<ReportInfo> ParseListChildrenResponse(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            
            var items = doc.Descendants(ns + "CatalogItem")
                          .Where(item => item.Element(ns + "Type")?.Value == "Report")
                          .Select(item => new ReportInfo
                          {
                              Name = item.Element(ns + "Name")?.Value ?? "",
                              Path = item.Element(ns + "Path")?.Value ?? "",
                              Type = item.Element(ns + "Type")?.Value ?? "",
                              CreatedDate = DateTime.TryParse(item.Element(ns + "CreationDate")?.Value, out var created) ? created : DateTime.MinValue,
                              ModifiedDate = DateTime.TryParse(item.Element(ns + "ModifiedDate")?.Value, out var modified) ? modified : DateTime.MinValue,
                              Description = item.Element(ns + "Description")?.Value ?? ""
                          });
            
            return items.ToList();
        }

        private IEnumerable<ReportParameter> ParseGetReportParametersResponse(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            
            var parameters = doc.Descendants(ns + "ReportParameter")
                               .Select(param => new ReportParameter
                               {
                                   Name = param.Element(ns + "Name")?.Value ?? "",
                                   Type = param.Element(ns + "Type")?.Value ?? "",
                                   Nullable = bool.TryParse(param.Element(ns + "Nullable")?.Value, out var nullable) && nullable,
                                   AllowBlank = bool.TryParse(param.Element(ns + "AllowBlank")?.Value, out var allowBlank) && allowBlank,
                                   MultiValue = bool.TryParse(param.Element(ns + "MultiValue")?.Value, out var multiValue) && multiValue,
                                   ValidValues = param.Elements(ns + "ValidValues")
                                                    .Elements(ns + "ValidValue")
                                                    .Select(vv => vv.Element(ns + "Value")?.Value ?? "")
                                                    .ToArray(),
                                   DefaultValue = param.Elements(ns + "DefaultValues")
                                                     .Elements(ns + "Value")
                                                     .FirstOrDefault()?.Value ?? "",
                                   Prompt = param.Element(ns + "Prompt")?.Value ?? ""
                               });
            
            return parameters.ToList();
        }

        private string ExtractExecutionId(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            
            return doc.Descendants(ns + "ExecutionID").FirstOrDefault()?.Value ?? "";
        }

        private byte[] ExtractRenderedReport(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            
            var resultElement = doc.Descendants(ns + "Result").FirstOrDefault();
            if (resultElement?.Value != null)
            {
                return Convert.FromBase64String(resultElement.Value);
            }
            
            throw new InvalidOperationException("Could not extract rendered report from response");
        }

        public void Dispose()
        {
            // No longer need to dispose _httpClient as we create them per-request
        }
    }
}