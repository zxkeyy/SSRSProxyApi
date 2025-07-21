```markdown
# SSRS Proxy API

This ASP.NET Core Web API provides a modern REST interface to SQL Server Reporting Services (SSRS) 2008, replacing the default web interface with a more flexible backend that can be consumed by modern frontend applications.

## Features

- **Windows Authentication**: Uses NTLM authentication to connect to SSRS
- **Three Main Endpoints**:
  - List reports from the root folder
  - Get report parameters
  - Render reports to PDF

## Configuration

Update your `appsettings.json` or `appsettings.Development.json` with your SSRS server details:

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
      "Domain": "YourDomain",
      "Username": "YourUsername",
      "Password": "YourPassword"
    },
    "Timeout": 300
  }
}
```

## API Endpoints

### 1. Get Reports
```
GET /api/reports
```
Returns a list of all reports in the root folder (/).

### 2. Get Report Parameters
```
GET /api/reports/{*path}/parameters
```
Returns the available parameters for a specific report.

Example: `GET /api/reports/MyFolder/MyReport/parameters`

### 3. Render Report
```
POST /api/reports/render
Content-Type: application/json

{
  "reportPath": "/MyFolder/MyReport",
  "parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-12-31",
    "Department": "Sales"
  }
}
```
Renders the report to PDF and returns the file.

## Authentication

The API uses Windows Authentication (Negotiate) by default. Make sure your application pool and IIS settings are configured to support Windows Authentication.

## Usage Example

```javascript
// Get reports
const reports = await fetch('/api/reports', {
  credentials: 'include'
}).then(r => r.json());

// Get parameters for a report
const parameters = await fetch('/api/reports/Sales Report/parameters', {
  credentials: 'include'
}).then(r => r.json());

// Render report
const pdfBlob = await fetch('/api/reports/render', {
  method: 'POST',
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    reportPath: '/Sales Report',
    parameters: {
      StartDate: '2024-01-01',
      EndDate: '2024-12-31'
    }
  })
}).then(r => r.blob());

// Download the PDF
const url = URL.createObjectURL(pdfBlob);
const a = document.createElement('a');
a.href = url;
a.download = 'report.pdf';
a.click();
```

## Development

The API is built with:
- .NET 8
- ASP.NET Core Web API
- System.ServiceModel (for SOAP communication)
- Windows Authentication

## Error Handling

All endpoints return appropriate HTTP status codes and error messages in JSON format for failed requests.
```