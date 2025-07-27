using Microsoft.Extensions.Options;
using SSRSProxyApi.Models;
using System.Net;
using System.Text;
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

        #region Public Methods

        public async Task<IEnumerable<ReportInfo>> GetReportsAsync(string folderPath = "/")
        {
            var folderContent = await BrowseFolderAsync(folderPath);
            return folderContent.Reports;
        }

        public async Task<FolderContent> BrowseFolderAsync(string folderPath = "/")
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogInformation("User '{User}' browsing folder: {FolderPath}", currentUser, folderPath);
                _logger.LogInformation("Using SSRS endpoint: {Endpoint}", _config.SoapEndpoints.ReportService);
                
                var soapEnvelope = CreateListChildrenSoapEnvelope(folderPath);
                _logger.LogDebug("SOAP Request: {SoapEnvelope}", soapEnvelope);
                
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                
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
                    _logger.LogError("SSRS Error Response for user '{User}': {StatusCode} - {ErrorContent}", currentUser, response.StatusCode, errorContent);
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("Authentication failed for user '{User}'. Check user permissions in SSRS.", currentUser);
                        
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
                
                var folderContent = ParseListChildrenResponseToFolderContent(responseContent, folderPath);
                _logger.LogInformation("Successfully retrieved {FolderCount} folders and {ReportCount} reports for user '{User}' from folder: {FolderPath}", 
                    folderContent.Folders.Count, folderContent.Reports.Count, currentUser, folderPath);
                
                return folderContent;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Error browsing folder for user '{User}': {FolderPath}", currentUser, folderPath);
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
                    
                    var (statusCode, errorCode, errorMessage) = ParseSSRSError(errorContent);
                    throw new SSRSException(statusCode, errorCode, $"Failed to retrieve parameters for report '{reportPath}': {errorMessage}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("SSRS Parameters Response: {Response}", responseContent);
                
                var parameters = ParseGetReportParametersResponse(responseContent);
                _logger.LogInformation("Successfully retrieved {ParameterCount} parameters for user '{User}' for report: {ReportPath}", 
                    parameters.Count(), currentUser, reportPath);
                
                return parameters;
            }
            catch (SSRSException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Error retrieving parameters for user '{User}' for report: {ReportPath}", currentUser, reportPath);
                throw new SSRSException(500, "UnexpectedError", $"An unexpected error occurred while retrieving parameters for report '{reportPath}'", ex);
            }
        }

        public async Task<byte[]> RenderReportAsync(string reportPath, Dictionary<string, object> parameters, string format = "PDF")
        {
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogInformation("User '{User}' attempting to render report: {ReportPath} in format: {Format}", currentUser, reportPath, format);
                
                using var sessionHttpClient = CreateSessionHttpClient();
                
                // Step 1: Load the report
                _logger.LogInformation("Step 1: Loading report '{ReportPath}' for user '{User}'", reportPath, currentUser);
                var executionId = await LoadReportAsync(sessionHttpClient, reportPath, currentUser);
                
                // Step 2: Set parameters if any
                if (parameters.Any())
                {
                    _logger.LogInformation("Step 2: Setting {ParameterCount} parameters for ExecutionID: {ExecutionId}", parameters.Count, executionId);
                    await SetExecutionParametersAsync(sessionHttpClient, executionId, parameters, currentUser);
                }
                else
                {
                    _logger.LogInformation("No parameters to set for report '{ReportPath}'", reportPath);
                }
                
                // Step 3: Render the report
                _logger.LogInformation("Step 3: Rendering report with ExecutionID: {ExecutionId} in format: {Format}", executionId, format);
                var reportBytes = await RenderReportWithExecutionIdAsync(sessionHttpClient, executionId, format, currentUser);
                
                _logger.LogInformation("Successfully rendered report for user '{User}': {ReportPath} in format {Format} ({Size} bytes)", 
                    currentUser, reportPath, format, reportBytes.Length);
                
                return reportBytes;
            }
            catch (SSRSException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
                _logger.LogError(ex, "Unexpected error rendering report for user '{User}': {ReportPath} in format: {Format}", currentUser, reportPath, format);
                throw new SSRSException(500, "UnexpectedError", $"An unexpected error occurred while rendering report '{reportPath}': {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<PolicyInfo>> GetPoliciesAsync(string itemPath)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateGetPoliciesSoapEnvelope(itemPath);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/GetPolicies");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to get policies for item '{itemPath}': {errorMessage}");
            }
            return ParseGetPoliciesResponse(responseContent);
        }

        public async Task SetPoliciesAsync(string itemPath, IEnumerable<PolicyInfo> policies)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateSetPoliciesSoapEnvelope(itemPath, policies);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/SetPolicies");
            Console.WriteLine($"envelope : {soapEnvelope}");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to set policies for item '{itemPath}': {errorMessage}");
            }
        }

        public async Task<IEnumerable<RoleInfo>> ListRolesAsync()
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateListRolesSoapEnvelope();
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/ListRoles");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to list roles: {errorMessage}");
            }
            return ParseListRolesResponse(responseContent);
        }

        public async Task CreateFolderAsync(string parentPath, string folderName, string description = "")
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateCreateFolderSoapEnvelope(parentPath, folderName, description);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService) { Content = content };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/CreateFolder");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to create folder: {errorMessage}");
            }
        }

        public async Task DeleteFolderAsync(string folderPath)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateDeleteItemSoapEnvelope(folderPath);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService) { Content = content };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/DeleteItem");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to delete folder: {errorMessage}");
            }
        }

        public async Task CreateReportAsync(string parentPath, string reportName, byte[] definition, string description = "")
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateCreateReportSoapEnvelope(parentPath, reportName, definition, description);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService) { Content = content };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/CreateReport");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to create report: {errorMessage}");
            }
        }

        public async Task DeleteReportAsync(string reportPath)
        {
            await DeleteFolderAsync(reportPath); // Reports and folders use DeleteItem
        }

        public async Task MoveItemAsync(string itemPath, string targetPath)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateMoveItemSoapEnvelope(itemPath, targetPath);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService) { Content = content };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/MoveItem");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to move item: {errorMessage}");
            }
        }

        public async Task<IEnumerable<RoleInfo>> ListSystemRolesAsync()
        {
            return await ListRolesByScopeAsync("System");
        }

        public async Task<IEnumerable<RoleInfo>> ListCatalogRolesAsync()
        {
            return await ListRolesByScopeAsync("Catalog");
        }

        public void Dispose()
        {
            // No resources to dispose
        }

        #endregion

        #region Private HTTP Client Methods

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

            if (OperatingSystem.IsWindows() && 
                currentUser?.Identity is WindowsIdentity windowsIdentity && 
                windowsIdentity.IsAuthenticated)
            {
                _logger.LogInformation("Creating HttpClient for authenticated Windows user: {UserName}", currentUserName);
                
                try
                {
                    handler.UseDefaultCredentials = true;
                    _logger.LogDebug("Using default credentials for user: {UserName}", currentUserName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to use Windows identity for user {UserName}, falling back to configured credentials", currentUserName);
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
        /// Creates an HttpClient with session management for report rendering
        /// </summary>
        private HttpClient CreateSessionHttpClient()
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer,
                UseDefaultCredentials = false,
                PreAuthenticate = true
            };
            
            var currentUserContext = _httpContextAccessor.HttpContext?.User;
            if (OperatingSystem.IsWindows() && 
                currentUserContext?.Identity is WindowsIdentity windowsIdentity && 
                windowsIdentity.IsAuthenticated)
            {
                var currentUser = currentUserContext.Identity.Name ?? "Anonymous";
                _logger.LogDebug("Using Windows credentials for SSRS session for user: {UserName}", currentUser);
                handler.UseDefaultCredentials = true;
            }
            else if (!string.IsNullOrEmpty(_config.Authentication.Username))
            {
                _logger.LogDebug("Using configured credentials for SSRS session");
                var credentialCache = new CredentialCache();
                var uri = new Uri(_config.SoapEndpoints.ReportExecution);
                
                var credential = new NetworkCredential(
                    _config.Authentication.Username,
                    _config.Authentication.Password,
                    _config.Authentication.Domain);
                
                credentialCache.Add(uri, "NTLM", credential);
                credentialCache.Add(uri, "Negotiate", credential);
                handler.Credentials = credentialCache;
            }
            else
            {
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

        #endregion

        #region Private Report Rendering Methods

        /// <summary>
        /// Loads a report and returns the execution ID
        /// </summary>
        private async Task<string> LoadReportAsync(HttpClient httpClient, string reportPath, string currentUser)
        {
            var loadSoapEnvelope = CreateLoadReportSoapEnvelope(reportPath);
            _logger.LogDebug("LoadReport SOAP request: {SoapEnvelope}", loadSoapEnvelope);
            
            var loadContent = CreateSoapContent(loadSoapEnvelope);
            var loadRequest = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportExecution)
            {
                Content = loadContent
            };
            loadRequest.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/LoadReport");
            
            var loadResponse = await httpClient.SendAsync(loadRequest);
            _logger.LogInformation("LoadReport response status: {StatusCode}", loadResponse.StatusCode);
            
            if (!loadResponse.IsSuccessStatusCode)
            {
                var errorContent = await loadResponse.Content.ReadAsStringAsync();
                _logger.LogError("LoadReport failed for user '{User}'. Status: {StatusCode}, Content: {ErrorContent}", currentUser, loadResponse.StatusCode, errorContent);
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(errorContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to load report '{reportPath}': {errorMessage}");
            }
            
            var loadResponseContent = await loadResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("LoadReport response content: {Response}", loadResponseContent);
            var executionId = ExtractExecutionId(loadResponseContent);
            
            if (string.IsNullOrEmpty(executionId))
            {
                _logger.LogError("Failed to extract ExecutionID from LoadReport response. Content: {Response}", loadResponseContent);
                throw new SSRSException(500, "MissingExecutionId", "Failed to extract execution ID from LoadReport response");
            }
            
            _logger.LogInformation("Successfully loaded report. ExecutionID: {ExecutionId}", executionId);
            return executionId;
        }

        /// <summary>
        /// Sets execution parameters for a report
        /// </summary>
        private async Task SetExecutionParametersAsync(HttpClient httpClient, string executionId, Dictionary<string, object> parameters, string currentUser)
        {
            var setParamsSoapEnvelope = CreateSetExecutionParametersSoapEnvelope(executionId, parameters);
            _logger.LogDebug("SetExecutionParameters SOAP request: {SoapEnvelope}", setParamsSoapEnvelope);
            
            var setParamsContent = CreateSoapContent(setParamsSoapEnvelope);
            var setParamsRequest = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportExecution)
            {
                Content = setParamsContent
            };
            setParamsRequest.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/SetExecutionParameters");
            
            var setParamsResponse = await httpClient.SendAsync(setParamsRequest);
            _logger.LogInformation("SetExecutionParameters response status: {StatusCode}", setParamsResponse.StatusCode);
            
            if (!setParamsResponse.IsSuccessStatusCode)
            {
                var errorContent = await setParamsResponse.Content.ReadAsStringAsync();
                _logger.LogError("SetExecutionParameters failed for user '{User}'. Status: {StatusCode}, Content: {ErrorContent}", currentUser, setParamsResponse.StatusCode, errorContent);
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(errorContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to set parameters: {errorMessage}");
            }
            
            _logger.LogInformation("Successfully set parameters for ExecutionID: {ExecutionId}", executionId);
        }

        /// <summary>
        /// Renders a report with the given execution ID
        /// </summary>
        private async Task<byte[]> RenderReportWithExecutionIdAsync(HttpClient httpClient, string executionId, string format, string currentUser)
        {
            var renderSoapEnvelope = CreateRenderSoapEnvelope(executionId, format);
            _logger.LogDebug("Render SOAP request: {SoapEnvelope}", renderSoapEnvelope);
            
            var renderContent = CreateSoapContent(renderSoapEnvelope);
            var renderRequest = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportExecution)
            {
                Content = renderContent
            };
            renderRequest.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/Render");
            
            var renderResponse = await httpClient.SendAsync(renderRequest);
            _logger.LogInformation("Render response status: {StatusCode}", renderResponse.StatusCode);
            
            if (!renderResponse.IsSuccessStatusCode)
            {
                var errorContent = await renderResponse.Content.ReadAsStringAsync();
                _logger.LogError("Render failed for user '{User}'. Status: {StatusCode}, Content: {ErrorContent}", currentUser, renderResponse.StatusCode, errorContent);
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(errorContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to render report in format '{format}': {errorMessage}");
            }
            
            var renderResponseContent = await renderResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("Render response content length: {Length} characters", renderResponseContent.Length);
            return ExtractRenderedReport(renderResponseContent);
        }

        /// <summary>
        /// Creates properly formatted SOAP content
        /// </summary>
        private static StringContent CreateSoapContent(string soapEnvelope)
        {
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            return content;
        }

        #endregion

        #region SOAP Envelope Creation

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
            <ForRendering>true</ForRendering>
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
    <soap:Header>
        <ExecutionHeader xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <ExecutionID>{executionId}</ExecutionID>
        </ExecutionHeader>
    </soap:Header>
    <soap:Body>
        <SetExecutionParameters xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Parameters>{paramXml}</Parameters>
            <ParameterLanguage>en-us</ParameterLanguage>
        </SetExecutionParameters>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateRenderSoapEnvelope(string executionId, string format = "PDF")
        {
            var ssrsFormat = format.ToUpper() switch
            {
                "PDF" => "PDF",
                "EXCEL" => "EXCELOPENXML",
                "WORD" => "WORDOPENXML", 
                "CSV" => "CSV",
                "XML" => "XML",
                "IMAGE" => "IMAGE",
                _ => "PDF"
            };

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Header>
        <ExecutionHeader xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <ExecutionID>{executionId}</ExecutionID>
        </ExecutionHeader>
    </soap:Header>
    <soap:Body>
        <Render xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Format>{ssrsFormat}</Format>
            <DeviceInfo></DeviceInfo>
        </Render>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateGetPoliciesSoapEnvelope(string itemPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <GetPolicies xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Item>{itemPath}</Item>
        </GetPolicies>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateSetPoliciesSoapEnvelope(string itemPath, IEnumerable<PolicyInfo> policies)
        {
            var policiesXml = string.Join("", policies.Select(p => $"<Policy><GroupUserName>{p.GroupUserName}</GroupUserName><Roles>{string.Join("", p.Roles.Select(r => $"<Role><Name>{r}</Name></Role>"))}</Roles></Policy>"));
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <SetPolicies xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Item>{itemPath}</Item>
            <Policies>{policiesXml}</Policies>
        </SetPolicies>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateListRolesSoapEnvelope(string? scope = null)
        {
            if (!string.IsNullOrEmpty(scope))
            {
                return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ListRoles xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <SecurityScope>{scope}</SecurityScope>
        </ListRoles>
    </soap:Body>
</soap:Envelope>";
            }
            else
            {
                return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <ListRoles xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"" />
    </soap:Body>
</soap:Envelope>";
            }
        }

        private string CreateCreateFolderSoapEnvelope(string parentPath, string folderName, string description)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <CreateFolder xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Folder>{folderName}</Folder>
            <Parent>{parentPath}</Parent>
            <Description>{description}</Description>
        </CreateFolder>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateCreateReportSoapEnvelope(string parentPath, string reportName, byte[] definition, string description)
        {
            var definitionBase64 = Convert.ToBase64String(definition);
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <CreateReport xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Report>{reportName}</Report>
            <Parent>{parentPath}</Parent>
            <Definition>{definitionBase64}</Definition>
            <Description>{description}</Description>
            <Overwrite>true</Overwrite>
        </CreateReport>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateDeleteItemSoapEnvelope(string itemPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <DeleteItem xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Item>{itemPath}</Item>
        </DeleteItem>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateMoveItemSoapEnvelope(string itemPath, string targetPath)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <MoveItem xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Item>{itemPath}</Item>
            <Target>{targetPath}</Target>
        </MoveItem>
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateGetSystemPoliciesSoapEnvelope()
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <GetSystemPolicies xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"" />
    </soap:Body>
</soap:Envelope>";
        }

        private string CreateSetSystemPoliciesSoapEnvelope(IEnumerable<PolicyInfo> policies)
        {
            var policiesXml = string.Join("", policies.Select(p => $"<Policy><GroupUserName>{p.GroupUserName}</GroupUserName><Roles>{string.Join("", p.Roles.Select(r => $"<Role><Name>{r}</Name></Role>"))}</Roles></Policy>"));
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <SetSystemPolicies xmlns=""http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices"">
            <Policies>{policiesXml}</Policies>
        </SetSystemPolicies>
    </soap:Body>
</soap:Envelope>";
        }

        #endregion

        #region Response Parsing

        /// <summary>
        /// Parse SSRS SOAP error response to determine appropriate HTTP status code and error details
        /// </summary>
        private (int statusCode, string errorCode, string errorMessage) ParseSSRSError(string errorContent)
        {
            try
            {
                _logger.LogDebug("Parsing SSRS error content: {ErrorContent}", errorContent);
                
                if (string.IsNullOrEmpty(errorContent))
                {
                    return (500, "UnknownError", "Unknown SSRS error occurred - empty response");
                }

                // Check for common SSRS error patterns
                if (errorContent.Contains("rsItemNotFound") || (errorContent.Contains("The item") && errorContent.Contains("cannot be found")))
                {
                    return (404, "ItemNotFound", "Report or folder not found");
                }
                
                if (errorContent.Contains("rsAccessDenied") || errorContent.Contains("not authorized"))
                {
                    return (403, "AccessDenied", "Access denied to the requested report");
                }
                
                if (errorContent.Contains("rsInvalidParameter") || (errorContent.Contains("parameter") && errorContent.Contains("invalid")))
                {
                    return (400, "InvalidParameter", "Invalid parameter provided");
                }
                
                if (errorContent.Contains("rsParameterTypeMismatch"))
                {
                    return (400, "ParameterTypeMismatch", "Parameter type mismatch");
                }

                if (errorContent.Contains("rsAuthenticationFailed") || errorContent.Contains("authentication"))
                {
                    return (401, "AuthenticationFailed", "Authentication failed");
                }

                // Extract error message from SOAP fault if available
                if (errorContent.Contains("<faultstring>"))
                {
                    var start = errorContent.IndexOf("<faultstring>") + "<faultstring>".Length;
                    var end = errorContent.IndexOf("</faultstring>");
                    if (end > start)
                    {
                        var faultString = errorContent.Substring(start, end - start);
                        faultString = System.Net.WebUtility.HtmlDecode(faultString);
                        
                        _logger.LogWarning("SSRS SOAP fault extracted: {FaultString}", faultString);
                        
                        if (faultString.Contains("not found") || faultString.Contains("does not exist"))
                        {
                            return (404, "ItemNotFound", faultString);
                        }
                        
                        return (400, "SSRSError", faultString);
                    }
                }

                // Check for HTTP-level errors
                if (errorContent.Contains("500") || errorContent.Contains("Internal Server Error"))
                {
                    return (500, "InternalServerError", "SSRS internal server error");
                }
                
                if (errorContent.Contains("404") || errorContent.Contains("Not Found"))
                {
                    return (404, "NotFound", "SSRS endpoint not found");
                }

                var truncatedError = errorContent.Length > 200 ? errorContent.Substring(0, 200) + "..." : errorContent;
                _logger.LogWarning("Unrecognized SSRS error pattern: {ErrorContent}", truncatedError);
                
                return (500, "UnrecognizedError", $"Unrecognized SSRS error: {truncatedError}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse SSRS error response");
                return (500, "ParseError", "Failed to parse SSRS error response");
            }
        }

        private FolderContent ParseListChildrenResponseToFolderContent(string responseXml, string folderPath)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            
            var catalogItems = doc.Descendants(ns + "CatalogItem");
            
            var folders = catalogItems
                .Where(item => item.Element(ns + "Type")?.Value == "Folder")
                .Select(item => new FolderInfo
                {
                    Name = item.Element(ns + "Name")?.Value ?? "",
                    Path = item.Element(ns + "Path")?.Value ?? "",
                    CreatedDate = DateTime.TryParse(item.Element(ns + "CreationDate")?.Value, out var created) ? created : DateTime.MinValue,
                    ModifiedDate = DateTime.TryParse(item.Element(ns + "ModifiedDate")?.Value, out var modified) ? modified : DateTime.MinValue,
                    Description = item.Element(ns + "Description")?.Value ?? ""
                })
                .ToList();

            var reports = catalogItems
                .Where(item => item.Element(ns + "Type")?.Value == "Report")
                .Select(item => new ReportInfo
                {
                    Name = item.Element(ns + "Name")?.Value ?? "",
                    Path = item.Element(ns + "Path")?.Value ?? "",
                    Type = item.Element(ns + "Type")?.Value ?? "",
                    CreatedDate = DateTime.TryParse(item.Element(ns + "CreationDate")?.Value, out var created) ? created : DateTime.MinValue,
                    ModifiedDate = DateTime.TryParse(item.Element(ns + "ModifiedDate")?.Value, out var modified) ? modified : DateTime.MinValue,
                    Description = item.Element(ns + "Description")?.Value ?? ""
                })
                .ToList();

            return new FolderContent
            {
                CurrentPath = folderPath,
                Folders = folders,
                Reports = reports
            };
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
            try
            {
                var doc = XDocument.Parse(responseXml);
                var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
                
                var executionId = doc.Descendants(ns + "ExecutionID").FirstOrDefault()?.Value;
                
                if (string.IsNullOrEmpty(executionId))
                {
                    executionId = doc.Descendants("ExecutionID").FirstOrDefault()?.Value;
                }
                
                return executionId ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract ExecutionID from response: {Response}", responseXml);
                return "";
            }
        }

        private byte[] ExtractRenderedReport(string responseXml)
        {
            try
            {
                var doc = XDocument.Parse(responseXml);
                var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
                
                var resultElement = doc.Descendants(ns + "Result").FirstOrDefault();
                if (resultElement?.Value != null)
                {
                    return Convert.FromBase64String(resultElement.Value);
                }
                
                resultElement = doc.Descendants("Result").FirstOrDefault();
                if (resultElement?.Value != null)
                {
                    return Convert.FromBase64String(resultElement.Value);
                }
                
                throw new InvalidOperationException("Could not extract rendered report from response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract rendered report from response: {Response}", responseXml);
                throw new InvalidOperationException("Could not extract rendered report from response", ex);
            }
        }

        private IEnumerable<PolicyInfo> ParseGetPoliciesResponse(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            var policies = doc.Descendants(ns + "Policy")
                .Select(p => new PolicyInfo
                {
                    GroupUserName = p.Element(ns + "GroupUserName")?.Value ?? string.Empty,
                    Roles = p.Element(ns + "Roles")?.Elements(ns + "Role").Select(r => r.Value).ToList() ?? new List<string>()
                });
            return policies.ToList();
        }

        private IEnumerable<RoleInfo> ParseListRolesResponse(string responseXml)
        {
            var doc = XDocument.Parse(responseXml);
            var ns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices");
            var roles = doc.Descendants(ns + "Role")
                .Select(r => new RoleInfo
                {
                    Name = r.Element(ns + "Name")?.Value ?? string.Empty,
                    Description = r.Element(ns + "Description")?.Value ?? string.Empty
                });
            return roles.ToList();
        }

        #endregion

        private async Task<IEnumerable<RoleInfo>> ListRolesByScopeAsync(string scope)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateListRolesSoapEnvelope(scope);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/ListRoles");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to list roles: {errorMessage}");
            }
            return ParseListRolesResponse(responseContent);
        }

        public async Task<IEnumerable<PolicyInfo>> GetSystemPoliciesAsync()
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateGetSystemPoliciesSoapEnvelope();
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/GetSystemPolicies");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to get system policies: {errorMessage}");
            }
            return ParseGetPoliciesResponse(responseContent);
        }

        public async Task SetSystemPoliciesAsync(IEnumerable<PolicyInfo> policies)
        {
            using var httpClient = CreateHttpClientForCurrentUser();
            var soapEnvelope = CreateSetSystemPoliciesSoapEnvelope(policies);
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SoapEndpoints.ReportService)
            {
                Content = content
            };
            request.Headers.Add("SOAPAction", "http://schemas.microsoft.com/sqlserver/2005/06/30/reporting/reportingservices/SetSystemPolicies");
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var (statusCode, errorCode, errorMessage) = ParseSSRSError(responseContent);
                throw new SSRSException(statusCode, errorCode, $"Failed to set system policies: {errorMessage}");
            }
        }
    }
}