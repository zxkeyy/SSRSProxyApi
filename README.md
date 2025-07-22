# SSRS Proxy API

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

A modern **ASP.NET Core Web API** that provides a RESTful interface to **SQL Server Reporting Services (SSRS)**, enabling modern web applications to interact with legacy SSRS reports through clean HTTP endpoints.

## ?? Features

- **?? Windows Authentication**: Seamless integration with existing Active Directory infrastructure
- **?? Folder Navigation**: Browse SSRS folder hierarchy with full metadata
- **?? Dynamic Report Rendering**: Support for multiple output formats (PDF, Excel, Word, CSV, XML, Images)
- **?? Parameter Management**: Automatic discovery and validation of report parameters
- **?? Session Management**: Proper SSRS session handling with ExecutionHeaders
- **??? Security**: Pass-through authentication preserving user permissions
- **?? Comprehensive Logging**: Detailed logging for debugging and monitoring
- **?? RESTful Design**: Clean, predictable API endpoints following REST principles

## ?? Table of Contents

- [Quick Start](#-quick-start)
- [Configuration](#-configuration)
- [API Documentation](#-api-documentation)
- [Authentication](#-authentication)
- [Examples](#-examples)
- [Development](#-development)
- [Troubleshooting](#-troubleshooting)
- [Contributing](#-contributing)

## ? Quick Start

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

3. **Update configuration** (see [Configuration](#-configuration) section)

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

## ?? Configuration

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
| `ReportServerUrl` | Base URL of your SSRS Report Server | ? | - |
| `ReportService` | SOAP endpoint for Report Service 2005 | ? | - |
| `ReportExecution` | SOAP endpoint for Report Execution 2005 | ? | - |
| `Authentication.Type` | Authentication type (Windows) | ? | Windows |
| `Authentication.Domain` | Windows domain (leave empty for pass-through) | ? | - |
| `Authentication.Username` | Service account username (optional) | ? | - |
| `Authentication.Password` | Service account password (optional) | ? | - |
| `Timeout` | Request timeout in seconds | ? | 300 |

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

## ?? API Documentation

### Base URL
```
https://localhost:7134/api/Reports
```

### Endpoints Overview

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/test-connection` | Test SSRS connectivity | ? |
| `GET` | `/browse` | Browse folder structure | ? |
| `GET` | `/` | Get reports (legacy) | ? |
| `GET` | `/parameters` | Get report parameters | ? |
| `POST` | `/render` | Render report as PDF | ? |
| `POST` | `/render/{format}` | Render report in specific format | ? |
| `GET` | `/user` | Get current user info | ? |

---

### ?? **Test Connection**
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

### ?? **Browse Folders**
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

### ?? **Get Report Parameters**
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

### ?? **Render Report (PDF)**
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

### ?? **Render Report (Multiple Formats)**
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

### ?? **Get Current User**
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

## ?? Authentication

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

## ?? Examples

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