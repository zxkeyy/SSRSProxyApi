using Microsoft.Extensions.Options;
using SSRSProxyApi.Models;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SSRSProxyApi.Services
{
    public class SSRSService : ISSRSService
    {
        private readonly SSRSConfig _config;
        private readonly ILogger<SSRSService> _logger;
        private readonly HttpClient _httpClient;

        public SSRSService(IOptions<SSRSConfig> config, ILogger<SSRSService> logger)
        {
            _config = config.Value;
            _logger = logger;
            
            // Create HttpClient with NTLM authentication
            var handler = new HttpClientHandler()
            {
                Credentials = new NetworkCredential(
                    _config.Authentication.Username,
                    _config.Authentication.Password,
                    _config.Authentication.Domain),
                PreAuthenticate = true
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("SOAPAction", "");
        }

        public async Task<IEnumerable<ReportInfo>> GetReportsAsync(string folderPath = "/")
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve reports from folder: {FolderPath}", folderPath);
                
                var soapEnvelope = CreateListChildrenSoapEnvelope(folderPath);
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                
                var response = await _httpClient.PostAsync(_config.SoapEndpoints.ReportService, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("SSRS Response: {Response}", responseContent);
                
                return ParseListChildrenResponse(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports from folder: {FolderPath}", folderPath);
                throw;
            }
        }

        public async Task<IEnumerable<ReportParameter>> GetReportParametersAsync(string reportPath)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve parameters for report: {ReportPath}", reportPath);
                
                var soapEnvelope = CreateGetReportParametersSoapEnvelope(reportPath);
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                
                var response = await _httpClient.PostAsync(_config.SoapEndpoints.ReportService, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("SSRS Parameters Response: {Response}", responseContent);
                
                return ParseGetReportParametersResponse(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving parameters for report: {ReportPath}", reportPath);
                throw;
            }
        }

        public async Task<byte[]> RenderReportAsync(string reportPath, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogInformation("Attempting to render report: {ReportPath}", reportPath);
                
                // First load the report
                var loadSoapEnvelope = CreateLoadReportSoapEnvelope(reportPath);
                var loadContent = new StringContent(loadSoapEnvelope, Encoding.UTF8, "text/xml");
                
                var loadResponse = await _httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, loadContent);
                loadResponse.EnsureSuccessStatusCode();
                
                var loadResponseContent = await loadResponse.Content.ReadAsStringAsync();
                var executionId = ExtractExecutionId(loadResponseContent);
                
                // Set parameters if any
                if (parameters.Any())
                {
                    var setParamsSoapEnvelope = CreateSetExecutionParametersSoapEnvelope(executionId, parameters);
                    var setParamsContent = new StringContent(setParamsSoapEnvelope, Encoding.UTF8, "text/xml");
                    
                    var setParamsResponse = await _httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, setParamsContent);
                    setParamsResponse.EnsureSuccessStatusCode();
                }
                
                // Render the report
                var renderSoapEnvelope = CreateRenderSoapEnvelope(executionId);
                var renderContent = new StringContent(renderSoapEnvelope, Encoding.UTF8, "text/xml");
                
                var renderResponse = await _httpClient.PostAsync(_config.SoapEndpoints.ReportExecution, renderContent);
                renderResponse.EnsureSuccessStatusCode();
                
                var renderResponseContent = await renderResponse.Content.ReadAsStringAsync();
                return ExtractRenderedReport(renderResponseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering report: {ReportPath}", reportPath);
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
            _httpClient?.Dispose();
        }
    }
}