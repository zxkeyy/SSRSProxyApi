# SSRS Proxy API

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

A modern **ASP.NET Core Web API** that provides a RESTful interface to **SQL Server Reporting Services (SSRS)**, enabling modern web applications to interact with legacy SSRS reports through clean HTTP endpoints.

## Features

- **:lock: Windows Authentication**: Seamless integration with existing Active Directory infrastructure
- **:file_folder: Folder Navigation**: Browse SSRS folder hierarchy with full metadata
- **:bar_chart: Dynamic Report Rendering**: Support for multiple output formats (PDF, Excel, Word, CSV, XML, Images)
- **:gear: Parameter Management**: Automatic discovery and validation of report parameters
- **:arrows_counterclockwise: Session Management**: Proper SSRS session handling with ExecutionHeaders
- **:shield: Security**: Pass-through authentication preserving user permissions
- **:memo: Comprehensive Logging**: Detailed logging for debugging and monitoring
- **:dart: RESTful Design**: Clean, predictable API endpoints following REST principles

## Table of Contents

- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Authentication](#authentication)
- [Examples](#examples)
- [Development](#development)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Quick Start

### Prerequisites

- **.NET 8.0** SDK or later
- **SQL Server Reporting Services (SSRS)** 2008 or later
- **Windows Server/IIS** with Windows Authentication enabled
- **Active Directory** environment (for authentication)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/ssrs-proxy-api.git
   cd ssrs-proxy-api
   ```

2. **Configure your SSRS settings**
   ```bash
   # Copy the example configuration
   cp appsettings.json appsettings.Development.json
   ```

3. **Update configuration** (see [Configuration](#configuration) section)

4. **Build and run**
   ```bash
   dotnet build
   dotnet run
   ```

5. **Test the API**
   ```bash
   # Test connectivity
   curl -X GET "https://localhost:7134/api/Reports/test-connection" -u "domain\username"
   ```

## Configuration

### Basic Configuration

Update your `appsettings.json` or `appsettings.Development.json`:

```json
{
  "SSRS": {
    "ReportServerUrl": "http://your-ssrs-server/ReportServer",
    "SoapEndpoints": {
      "ReportService": "http://your-ssrs-server/ReportServer/ReportService2005.asmx",
      "ReportExecution": "http://your-ssrs-server/ReportServer/ReportExecution2005.asmx"
    },
    "Authentication": {
      "Type": "Windows",
      "Domain": "",
      "Username": "",
      "Password": ""
    },
    "Timeout": 300
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SSRSProxyApi": "Debug"
    }
  }
}
```

### Configuration Options

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| `ReportServerUrl` | Base URL of your SSRS Report Server | :white_check_mark: | - |
| `ReportService` | SOAP endpoint for Report Service 2005 | :white_check_mark: | - |
| `ReportExecution` | SOAP endpoint for Report Execution 2005 | :white_check_mark: | - |
| `Authentication.Type` | Authentication type (Windows) | :white_check_mark: | Windows |
| `Authentication.Domain` | Windows domain (leave empty for pass-through) | :x: | - |
| `Authentication.Username` | Service account username (optional) | :x: | - |
| `Authentication.Password` | Service account password (optional) | :x: | - |
| `Timeout` | Request timeout in seconds | :x: | 300 |

### Authentication Modes

#### 1. **Pass-through Authentication** (Recommended)
```json
{
  "Authentication": {
    "Type": "Windows",
    "Domain": "",
    "Username": "",
    "Password": ""
  }
}
```
Uses the current user's Windows credentials. Requires Windows Authentication on IIS.

#### 2. **Service Account Authentication**
```json
{
  "Authentication": {
    "Type": "Windows",
    "Domain": "YOURDOMAIN",
    "Username": "ServiceAccount",
    "Password": "SecurePassword123"
  }
}
```
Uses a dedicated service account for all SSRS operations.

## API Documentation

### Base URL
```
https://localhost:7134/api/Reports
```

### Endpoints Overview

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/test-connection` | Test SSRS connectivity | :white_check_mark: |
| `GET` | `/browse` | Browse folder structure | :white_check_mark: |
| `GET` | `/` | Get reports (legacy) | :white_check_mark: |
| `GET` | `/parameters` | Get report parameters | :white_check_mark: |
| `POST` | `/render` | Render report as PDF | :white_check_mark: |
| `POST` | `/render/{format}` | Render report in specific format | :white_check_mark: |
| `GET` | `/user` | Get current user info | :white_check_mark: |

---

### **Test Connection**
Check SSRS connectivity and view available reports.

```http
GET /api/Reports/test-connection
```

**Response:**
```json
{
  "message": "SSRS connection successful",
  "user": "DOMAIN\\username",
  "reportCount": 15,
  "folderCount": 3,
  "reports": [
    { "name": "Sales Report", "path": "/Sales Report" },
    { "name": "Inventory Report", "path": "/Inventory Report" }
  ],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

### **Browse Folders**
Navigate SSRS folder hierarchy with full metadata.

```http
GET /api/Reports/browse?folderPath=/Sales
```

**Parameters:**
- `folderPath` (query, optional): Folder path to browse. Default: `/`

**Response:**
```json
{
  "currentPath": "/Sales",
  "folders": [
    {
      "name": "Regional Reports",
      "path": "/Sales/Regional Reports",
      "createdDate": "2024-01-01T00:00:00Z",
      "modifiedDate": "2024-01-10T15:30:00Z",
      "description": "Regional sales analysis reports"
    }
  ],
  "reports": [
    {
      "name": "Monthly Sales",
      "path": "/Sales/Monthly Sales",
      "type": "Report",
      "createdDate": "2024-01-01T00:00:00Z",
      "modifiedDate": "2024-01-10T15:30:00Z",
      "description": "Monthly sales performance report"
    }
  ]
}
```

---

### **Get Report Parameters**
Retrieve available parameters for a specific report.

```http
GET /api/Reports/parameters?reportPath=/Sales/Monthly%20Sales
```

**Parameters:**
- `reportPath` (query, required): Full path to the report

**Response:**
```json
[
  {
    "name": "StartDate",
    "type": "DateTime",
    "nullable": false,
    "allowBlank": false,
    "multiValue": false,
    "validValues": [],
    "defaultValue": "2024-01-01",
    "prompt": "Start Date"
  },
  {
    "name": "Department",
    "type": "String",
    "nullable": true,
    "allowBlank": true,
    "multiValue": true,
    "validValues": ["Sales", "Marketing", "Support"],
    "defaultValue": "Sales",
    "prompt": "Department(s)"
  }
]
```

---

### **Render Report (PDF)**
Render a report as PDF with parameters.

```http
POST /api/Reports/render
Content-Type: application/json

{
  "reportPath": "/Sales/Monthly Sales",
  "parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-01-31",
    "Department": "Sales"
  }
}
```

**Request Body:**
```json
{
  "reportPath": "string (required)",
  "parameters": {
    "key1": "value1",
    "key2": "value2"
  }
}
```

**Response:** Binary PDF file with appropriate headers

---

### **Render Report (Multiple Formats)**
Render a report in various formats.

```http
POST /api/Reports/render/{format}
Content-Type: application/json

{
  "reportPath": "/Sales/Monthly Sales",
  "parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-01-31"
  }
}
```

**Supported Formats:**
- `PDF` - Adobe PDF format
- `EXCEL` - Microsoft Excel (.xlsx)
- `WORD` - Microsoft Word (.docx)
- `CSV` - Comma-separated values
- `XML` - XML format
- `IMAGE` - PNG image format

**Response:** Binary file in requested format

---

### **Get Current User**
Retrieve information about the authenticated user.

```http
GET /api/Reports/user
```

**Response:**
```json
{
  "isAuthenticated": true,
  "name": "DOMAIN\\username",
  "authenticationType": "Negotiate",
  "isWindowsIdentity": true
}
```

## Authentication

The API uses **Windows Authentication (Negotiate)** by default, supporting:

- **NTLM Authentication**
- **Kerberos Authentication**
- **Pass-through credentials**
- **Service account delegation**

### Client Examples

#### JavaScript (Fetch API)
```javascript
// Include credentials for Windows Authentication
const response = await fetch('/api/Reports/browse', {
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json'
  }
});
```

#### C# (HttpClient)
```csharp
using var handler = new HttpClientHandler()
{
    UseDefaultCredentials = true
};
using var client = new HttpClient(handler);

var response = await client.GetAsync("https://localhost:7134/api/Reports/browse");
```

#### PowerShell
```powershell
# Using current user credentials
Invoke-RestMethod -Uri "https://localhost:7134/api/Reports/browse" -UseDefaultCredentials

# Using specific credentials
$cred = Get-Credential
Invoke-RestMethod -Uri "https://localhost:7134/api/Reports/browse" -Credential $cred
```

#### cURL
```bash
# Windows (current user)
curl -X GET "https://localhost:7134/api/Reports/browse" --negotiate -u :

# Unix/Linux
curl -X GET "https://localhost:7134/api/Reports/browse" -u "domain\\username:password"
```

## Examples

### Complete Workflow Example

```javascript
class SSRSClient {
  constructor(baseUrl) {
    this.baseUrl = baseUrl;
    this.headers = {
      'Content-Type': 'application/json'
    };
  }

  async testConnection() {
    const response = await fetch(`${this.baseUrl}/test-connection`, {
      credentials: 'include'
    });
    return response.json();
  }

  async browseFolder(folderPath = '/') {
    const response = await fetch(`${this.baseUrl}/browse?folderPath=${encodeURIComponent(folderPath)}`, {
      credentials: 'include'
    });
    return response.json();
  }

  async getReportParameters(reportPath) {
    const response = await fetch(`${this.baseUrl}/parameters?reportPath=${encodeURIComponent(reportPath)}`, {
      credentials: 'include'
    });
    return response.json();
  }

  async renderReport(reportPath, parameters = {}, format = 'PDF') {
    const endpoint = format.toUpperCase() === 'PDF' ? '/render' : `/render/${format}`;
    
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'POST',
      credentials: 'include',
      headers: this.headers,
      body: JSON.stringify({
        reportPath,
        parameters
      })
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(`Report rendering failed: ${error.message}`);
    }

    return response.blob();
  }

  async downloadReport(reportPath, parameters, filename, format = 'PDF') {
    try {
      const blob = await this.renderReport(reportPath, parameters, format);
      
      // Create download link
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      
      return true;
    } catch (error) {
      console.error('Download failed:', error);
      return false;
    }
  }
}

// Usage
const ssrs = new SSRSClient('https://localhost:7134/api/Reports');

// Test connection
const status = await ssrs.testConnection();
console.log('SSRS Status:', status);

// Browse reports
const reports = await ssrs.browseFolder('/Sales');
console.log('Available reports:', reports);

// Get parameters for a specific report
const parameters = await ssrs.getReportParameters('/Sales/Monthly Report');
console.log('Report parameters:', parameters);

// Render and download report
await ssrs.downloadReport(
  '/Sales/Monthly Report',
  {
    StartDate: '2024-01-01',
    EndDate: '2024-01-31',
    Department: 'Sales'
  },
  'monthly-sales-report.pdf'
);
```

### React Hook Example

```jsx
import { useState, useEffect } from 'react';

export function useSSRSReports() {
  const [reports, setReports] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchReports = async (folderPath = '/') => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/Reports/browse?folderPath=${encodeURIComponent(folderPath)}`, {
        credentials: 'include'
      });
      
      if (!response.ok) {
        throw new Error(`Failed to fetch reports: ${response.statusText}`);
      }
      
      const data = await response.json();
      setReports(data.reports);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const renderReport = async (reportPath, parameters = {}, format = 'PDF') => {
    const endpoint = format.toUpperCase() === 'PDF' ? '/render' : `/render/${format}`;
    
    const response = await fetch(`/api/Reports${endpoint}`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        reportPath,
        parameters
      })
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message);
    }

    return response.blob();
  };

  useEffect(() => {
    fetchReports();
  }, []);

  return {
    reports,
    loading,
    error,
    fetchReports,
    renderReport
  };
}

// Component usage
function ReportsList() {
  const { reports, loading, error, renderReport } = useSSRSReports();

  const handleDownload = async (reportPath) => {
    try {
      const blob = await renderReport(reportPath);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${reportPath.split('/').pop()}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      alert(`Download failed: ${error.message}`);
    }
  };

  if (loading) return <div>Loading reports...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      <h2>Available Reports</h2>
      <ul>
        {reports.map(report => (
          <li key={report.path}>
            <span>{report.name}</span>
            <button onClick={() => handleDownload(report.path)}>
              Download PDF
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

## Development

### Project Structure

```
SSRSProxyApi/
??? Controllers/
?   ??? ReportsController.cs      # API endpoints
??? Services/
?   ??? ISSRSService.cs           # Service interface
?   ??? SSRSService.cs            # SSRS integration logic
??? Models/
?   ??? ReportModels.cs           # Data models
?   ??? SSRSConfig.cs             # Configuration models
??? Program.cs                    # Application startup
??? appsettings.json              # Configuration
??? SSRSProxyApi.csproj          # Project file
```

### Key Dependencies

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Negotiate" Version="8.0.18" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
<PackageReference Include="System.ServiceModel.Http" Version="8.1.2" />
<PackageReference Include="System.ServiceModel.Primitives" Version="8.1.2" />
```

### Building and Testing

```bash
# Build the project
dotnet build

# Run tests (if available)
dotnet test

# Run the application
dotnet run

# Publish for deployment
dotnet publish -c Release -o ./publish
```

### Development Setup

1. **Install dependencies**
   ```bash
   dotnet restore
   ```

2. **Configure development settings**
   ```bash
   # Create development config
   cp appsettings.json appsettings.Development.json
   ```

3. **Enable detailed logging**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "SSRSProxyApi": "Debug"
       }
     }
   }
   ```

4. **Run in development mode**
   ```bash
   dotnet run --environment Development
   ```

### API Documentation

When running in development mode, Swagger documentation is available at:
- **Swagger UI**: `https://localhost:7134/swagger`
- **OpenAPI Spec**: `https://localhost:7134/swagger/v1/swagger.json`

## Troubleshooting

### Common Issues

#### 1. **Authentication Failures**

**Problem:** Getting 401 Unauthorized errors

**Solutions:**
- Ensure Windows Authentication is enabled in IIS
- Check that the application pool identity has access to SSRS
- Verify SSRS permissions for the user/service account
- Test with `curl --negotiate -u :` for credential validation

#### 2. **Report Not Found (404)**

**Problem:** Reports return "Item not found" errors

**Solutions:**
- Verify the report path is correct (case-sensitive)
- Check user permissions in SSRS Report Manager
- Ensure the report exists and is deployed
- Test with SSRS web interface first

#### 3. **Session Management Issues**

**Problem:** "Session identifier is missing" errors

**Solutions:**
- Check that `ReportExecution` endpoint URL is correct
- Verify SOAP endpoints are accessible
- Ensure proper ExecutionHeader is included in requests
- Check for network connectivity issues

#### 4. **Parameter Validation Errors**

**Problem:** Invalid parameter errors when rendering

**Solutions:**
- Use `/parameters` endpoint to get valid parameter names and types
- Ensure parameter values match expected data types
- Check for required parameters that are missing
- Validate date formats (ISO 8601 recommended)

#### 5. **Large Report Timeouts**

**Problem:** Reports timeout during rendering

**Solutions:**
- Increase the `Timeout` setting in configuration
- Optimize report queries and data sources
- Consider pagination for large datasets
- Implement async processing for heavy reports

### Debug Logging

Enable detailed logging to troubleshoot issues:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SSRSProxyApi.Services.SSRSService": "Debug"
    }
  }
}
```

This will log:
- SOAP request/response content
- Authentication details
- Step-by-step rendering process
- Error details and stack traces

### Performance Optimization

1. **Connection Pooling**: The API reuses HTTP connections efficiently
2. **Session Management**: Proper SSRS session handling reduces overhead
3. **Async Operations**: All operations are asynchronous
4. **Memory Optimization**: Streams are used for large report files

## Deployment

### IIS Deployment

1. **Publish the application**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Configure IIS**
   - Create a new Application Pool (.NET CLR Version: No Managed Code)
   - Create a new Website/Application
   - Enable Windows Authentication
   - Disable Anonymous Authentication

3. **Set permissions**
   ```bash
   # Grant IIS_IUSRS read permissions to application folder
   icacls "C:\inetpub\wwwroot\ssrs-proxy-api" /grant IIS_IUSRS:R /T
   ```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY ./publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "SSRSProxyApi.dll"]
```

### Environment Variables

For production deployment, use environment variables for sensitive configuration:

```bash
# Set SSRS configuration via environment variables
export SSRS__ReportServerUrl="http://prod-ssrs-server/ReportServer"
export SSRS__Authentication__Username="ProductionServiceAccount"
export SSRS__Authentication__Password="SecurePassword123"
```

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](docs/CONTRIBUTING.md) for details.

### Development Workflow

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass (`dotnet test`)
6. Commit your changes (`git commit -am 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- SOAP integration via [System.ServiceModel](https://docs.microsoft.com/en-us/dotnet/framework/wcf/)
- Authentication powered by [Microsoft.AspNetCore.Authentication.Negotiate](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)

## Support

- **Documentation**: Check this README and inline code comments
- **Issues**: Report bugs and request features via [GitHub Issues](https://github.com/yourusername/ssrs-proxy-api/issues)
- **Discussions**: Join conversations in [GitHub Discussions](https://github.com/yourusername/ssrs-proxy-api/discussions)

---

**Made with :heart: for the SSRS community**