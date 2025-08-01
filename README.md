# SSRS Proxy API

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

A modern **ASP.NET Core Web API** that provides a RESTful interface to **SQL Server Reporting Services (SSRS)**, enabling modern web applications to interact with legacy SSRS reports through clean HTTP endpoints.

## Features

- **🔒 Windows Authentication**: Seamless integration with existing Active Directory infrastructure
- **🗂️ Folder Navigation**: Browse SSRS folder hierarchy with full metadata
- **📊 Dynamic Report Rendering**: Support for multiple output formats (PDF, Excel, Word, CSV, XML, Images)
- **⚙️ Parameter Management**: Automatic discovery and validation of report parameters
- **🔄 Session Management**: Proper SSRS session handling with ExecutionHeaders
- **🛡️ Security Management**: Complete SSRS permissions and role management
- **📝 Report Management**: Create, delete, and move reports and folders
- **🔍 Search Functionality**: Recursive search across reports and folders
- **🚀 Demo Mode**: Development-friendly mode that bypasses authentication
- **📋 Comprehensive Logging**: Detailed logging for debugging and monitoring
- **🎯 RESTful Design**: Clean, predictable API endpoints following REST principles

## Frontend

A modern React frontend is available for this API, providing a user-friendly interface for browsing, searching, and rendering SSRS reports.

- **Repository:** https://github.com/zxkeyy/SSRSUI
- **Features:** Authentication, report browsing, parameter input, export/download, and more.
- **Quick Start:** See the frontend README for setup instructions.

The frontend communicates with this API via HTTP and is designed for seamless integration.

## Table of Contents

- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Authentication](#authentication)
- [Development](#development)
- [Troubleshooting](#troubleshooting)

## Quick Start

### Prerequisites

- **.NET 8.0** SDK or later
- **SQL Server Reporting Services (SSRS)** 2008 or later
- **Windows Server/IIS** with Windows Authentication enabled (for production)
- **Active Directory** environment (for authentication, unless using demo mode)

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
   # Test connectivity (with authentication)
   curl -X GET "https://localhost:7134/api/Reports/test-connection" -u "domain\username"
   
   # Test connectivity (demo mode)
   curl -X GET "https://localhost:7134/api/Reports/test-connection"
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
    "Timeout": 300,
    "IsDemo": false
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
| `ReportServerUrl` | Base URL of your SSRS Report Server | ✅ | - |
| `SoapEndpoints.ReportService` | SOAP endpoint for Report Service 2005 | ✅ | - |
| `SoapEndpoints.ReportExecution` | SOAP endpoint for Report Execution 2005 | ✅ | - |
| `Authentication.Type` | Authentication type (Windows) | ✅ | Windows |
| `Authentication.Domain` | Windows domain (leave empty for pass-through) | ❌ | - |
| `Authentication.Username` | Service account username (optional) | ❌ | - |
| `Authentication.Password` | Service account password (optional) | ❌ | - |
| `IsDemo` | Enable demo mode (bypasses user auth, uses service account) | ❌ | false |
| `Timeout` | Request timeout in seconds | ❌ | 300 |

### Authentication Modes

#### 1. **Pass-through Authentication** (Recommended for Production)
```json
{
  "Authentication": {
    "Type": "Windows",
    "Domain": "",
    "Username": "",
    "Password": ""
  },
  "IsDemo": false
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
  },
  "IsDemo": false
}
```
Uses a dedicated service account for all SSRS operations.

#### 3. **Demo Mode** (Development/Testing)
```json
{
  "Authentication": {
    "Type": "Windows",
    "Domain": "YOURDOMAIN",
    "Username": "ServiceAccount",
    "Password": "SecurePassword123"
  },
  "IsDemo": true
}
```
**Demo mode bypasses user authentication** and always uses the configured service account credentials. This is useful for:
- Development environments where Windows Authentication is not available
- Testing scenarios
- Demos and presentations
- Non-Windows hosting environments
- Swagger UI testing without authentication prompts

⚠️ **Warning**: Only use demo mode in development or testing environments. Do not enable this in production as it bypasses security controls.

## API Documentation

### Base URLs
```
Reports:     https://localhost:7134/api/Reports
Security:    https://localhost:7134/api/Security  
Management:  https://localhost:7134/api/Management
```

### Complete Endpoints Overview

#### Reports Controller (`/api/Reports`)
| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/test-connection` | Test SSRS connectivity | ✅* |
| `GET` | `/browse` | Browse folder structure | ✅* |
| `GET` | `/` | Get reports (legacy) | ✅* |
| `GET` | `/parameters` | Get report parameters | ✅* |
| `POST` | `/render` | Render report as PDF | ✅* |
| `POST` | `/render/{format}` | Render report in specific format | ✅* |
| `GET` | `/user` | Get current user info | ✅* |
| `GET` | `/search` | Search reports and folders | ✅* |

#### Security Controller (`/api/Security`)
| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/policies` | List item policies | ✅* |
| `POST` | `/policies` | Set item policies | ✅* |
| `GET` | `/roles` | List all available roles | ✅* |
| `GET` | `/roles/system` | List system roles | ✅* |
| `GET` | `/roles/catalog` | List catalog roles | ✅* |
| `GET` | `/policies/user` | Get user/group permissions | ✅* |
| `GET` | `/policies/system` | Get system policies | ✅* |
| `POST` | `/policies/system` | Set system policies | ✅* |

#### Management Controller (`/api/Management`)
| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `POST` | `/folder` | Create a new folder | ✅* |
| `DELETE` | `/folder` | Delete an existing folder | ✅* |
| `POST` | `/report` | Create a new report | ✅* |
| `DELETE` | `/report` | Delete an existing report | ✅* |
| `POST` | `/move` | Move item to new location | ✅* |

*Auth Required: Authentication is bypassed when `IsDemo: true` is configured.

---

See `docs/API_REFERENCE.md` for detailed endpoint documentation with request/response examples.

## Authentication

The API supports flexible authentication modes:

### Production Mode (`IsDemo: false`)
- **Windows Authentication (Negotiate)** with NTLM/Kerberos support
- **Pass-through credentials** preserving user permissions
- **Service account delegation** for centralized access

### Demo Mode (`IsDemo: true`)
- **No authentication required** - perfect for development
- **Service account credentials** used for all operations
- **Swagger UI works without authentication prompts**
- **Consistent user context** across all requests
   

## Development

### Project Structure

```
SSRSProxyApi/
├── Controllers/
│   ├── ReportsController.cs      # Report operations
│   ├── SecurityController.cs     # Security management
│   └── ManagementController.cs   # Report/folder CRUD
├── Services/
│   ├── ISSRSService.cs          # Service interface
│   ├── SSRSService.cs           # SSRS integration logic
│   ├── IUserInfoService.cs      # User info interface
│   └── UserInfoService.cs       # User info service
├── Models/
│   ├── ReportModels.cs          # Data models
│   └── SSRSConfig.cs            # Configuration models
├── Attributes/
│   └── ConditionalAuthorizeAttribute.cs  # Demo mode auth
├── wwwroot/                     # Static web files
├── Program.cs                   # Application startup
├── appsettings.json             # Configuration
└── SSRSProxyApi.csproj         # Project file
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
   # Copy and edit development config
   cp appsettings.json appsettings.Development.json
   ```

3. **Enable demo mode for development**
   ```json
   {
     "SSRS": {
       "Authentication": {
         "Domain": "YOURDOMAIN",
         "Username": "ServiceAccount",
         "Password": "Password123"
       },
       "IsDemo": true
     }
   }
   ```

4. **Enable detailed logging**
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

5. **Run in development mode**
   ```bash
   dotnet run --environment Development
   ```

### API Documentation

When running in development mode, Swagger documentation is available at:
- **Swagger UI**: `https://localhost:7134/swagger`
- **OpenAPI Spec**: `https://localhost:7134/swagger/v1/swagger.json`

In demo mode, Swagger UI will show "DEMO MODE ENABLED" and won't require authentication.

## Troubleshooting

### Common Issues

#### 1. **Authentication Failures**

**Problem:** Getting 401 Unauthorized errors

**Solutions:**
- **For Production**: Ensure Windows Authentication is enabled in IIS
- **For Development**: Enable demo mode (`"IsDemo": true`)
- Check that the application pool identity has access to SSRS
- Verify SSRS permissions for the user/service account
- Test with `curl --negotiate -u :` for credential validation

#### 2. **Demo Mode Issues**

**Problem:** Demo mode not working or still asking for credentials

**Solutions:**
- Verify `"IsDemo": true` is set in configuration
- Ensure service account credentials are configured
- Check that the service account has SSRS permissions
- Restart the application after configuration changes

#### 3. **Swagger UI Authentication**

**Problem:** Swagger UI keeps asking for credentials

**Solutions:**
- Enable demo mode for development: `"IsDemo": true`
- Check that conditional authorization is working
- Verify the `ConditionalAuthorizeAttribute` is properly registered

#### 4. **Report Not Found (404)**

**Problem:** Reports return "Item not found" errors

**Solutions:**
- Verify the report path is correct (case-sensitive)
- Check user permissions in SSRS Report Manager
- Ensure the report exists and is deployed
- Test with SSRS web interface first

#### 5. **Session Management Issues**

**Problem:** "Session identifier is missing" errors

**Solutions:**
- Check that `ReportExecution` endpoint URL is correct
- Verify SOAP endpoints are accessible
- Ensure proper ExecutionHeader is included in requests
- Check for network connectivity issues

#### 6. **Parameter Validation Errors**

**Problem:** Invalid parameter errors when rendering

**Solutions:**
- Use `/parameters` endpoint to get valid parameter names and types
- Ensure parameter values match expected data types
- Check for required parameters that are missing
- Validate date formats (ISO 8601 recommended)

#### 7. **Large Report Timeouts**

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
      "SSRSProxyApi.Services.SSRSService": "Debug",
      "SSRSProxyApi.Services.UserInfoService": "Debug"
    }
  }
}
```

This will log:
- SOAP request/response content
- Authentication details
- Demo mode status
- Step-by-step rendering process
- Error details and stack traces

### Performance Optimization

1. **Connection Pooling**: The API reuses HTTP connections efficiently
2. **Session Management**: Proper SSRS session handling reduces overhead
3. **Async Operations**: All operations are asynchronous
4. **Memory Optimization**: Streams are used for large report files
5. **Conditional Authorization**: Minimal overhead for demo mode

## Deployment

### IIS Deployment (Production)

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

4. **Production configuration**
   ```json
   {
     "SSRS": {
       "IsDemo": false,
       "Authentication": {
         "Type": "Windows"
       }
     }
   }
   ```

```

### Environment Variables

For production deployment, use environment variables for sensitive configuration:

```bash
# Set SSRS configuration via environment variables
export SSRS__ReportServerUrl="http://prod-ssrs-server/ReportServer"
export SSRS__Authentication__Username="ProductionServiceAccount"
export SSRS__Authentication__Password="SecurePassword123"
export SSRS__IsDemo="false"
```
