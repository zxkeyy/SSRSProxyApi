# SSRS Proxy API Reference

## Overview

The SSRS Proxy API provides RESTful endpoints to interact with SQL Server Reporting Services. All endpoints require Windows Authentication and return JSON responses (except for report rendering which returns binary content).

## Base URL

```
https://your-server/api
```

## Authentication

All endpoints require Windows Authentication using the Negotiate scheme (NTLM/Kerberos).

### Request Headers

```http
Authorization: Negotiate <token>
Content-Type: application/json
```

## Common Response Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Authentication required |
| 403 | Forbidden - Access denied to resource |
| 404 | Not Found - Report/folder not found |
| 500 | Internal Server Error |

## Error Response Format

```json
{
  "message": "Error description",
  "errorCode": "ErrorType",
  "user": "DOMAIN\\username",
  "reportPath": "/path/to/report",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

## Reports Controller Endpoints

Base path: `/api/Reports`

### Test Connection

Test SSRS connectivity and get basic server information.

```http
GET /api/Reports/test-connection
```

#### Response

```json
{
  "message": "SSRS connection successful",
  "user": "DOMAIN\\username",
  "reportCount": 15,
  "folderCount": 3,
  "reports": [
    {
      "name": "Sample Report",
      "path": "/Sample Report"
    }
  ],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

### Browse Folders

Navigate the SSRS folder structure with both folders and reports.

```http
GET /api/Reports/browse?folderPath={path}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| folderPath | string | No | Folder path to browse. Default: "/" |

#### Response

```json
{
  "currentPath": "/Sales",
  "folders": [
    {
      "name": "Regional",
      "path": "/Sales/Regional",
      "createdDate": "2024-01-01T00:00:00Z",
      "modifiedDate": "2024-01-10T15:30:00Z",
      "description": "Regional reports folder"
    }
  ],
  "reports": [
    {
      "name": "Monthly Sales",
      "path": "/Sales/Monthly Sales",
      "type": "Report",
      "createdDate": "2024-01-01T00:00:00Z",
      "modifiedDate": "2024-01-10T15:30:00Z",
      "description": "Monthly sales report"
    }
  ]
}
```

---

### Get Reports (Legacy)

Legacy endpoint to get reports from a specific folder.

```http
GET /api/Reports?folderPath={path}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| folderPath | string | No | Folder path. Default: "/" |

#### Response

```json
[
  {
    "name": "Sales Report",
    "path": "/Sales Report",
    "type": "Report",
    "createdDate": "2024-01-01T00:00:00Z",
    "modifiedDate": "2024-01-10T15:30:00Z",
    "description": "Monthly sales analysis"
  }
]
```

---

### Get Report Parameters

Retrieve parameter definitions for a specific report.

```http
GET /api/Reports/parameters?reportPath={path}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| reportPath | string | Yes | Full path to the report |

#### Response

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
    "validValues": ["Sales", "Marketing", "IT"],
    "defaultValue": "Sales",
    "prompt": "Department"
  }
]
```

#### Parameter Types

| Type | Description | Example Values |
|------|-------------|----------------|
| String | Text value | "Sales", "Marketing" |
| Integer | Whole number | 123, -456 |
| Float | Decimal number | 123.45, -67.89 |
| DateTime | Date and time | "2024-01-15T10:30:00Z" |
| Boolean | True/false | true, false |

---

### Render Report (PDF)

Render a report as PDF with specified parameters.

```http
POST /api/Reports/render
Content-Type: application/json

{
  "reportPath": "/Sales/Monthly Report",
  "parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-01-31",
    "Department": "Sales"
  }
}
```

#### Request Body

```json
{
  "reportPath": "string (required) - Full path to the report",
  "parameters": "object (optional) - Key-value pairs of report parameters"
}
```

#### Response

Binary PDF content with headers:

```http
Content-Type: application/pdf
Content-Disposition: attachment; filename="Report_20240115_103000.pdf"
```

---

### Render Report (Custom Format)

Render a report in a specific format.

```http
POST /api/Reports/render/{format}
Content-Type: application/json

{
  "reportPath": "/Sales/Monthly Report",
  "parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-01-31"
  }
}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| format | string | Yes | Output format (PDF, EXCEL, WORD, CSV, XML, IMAGE) |

#### Supported Formats

| Format | File Extension | MIME Type |
|--------|----------------|-----------|
| PDF | .pdf | application/pdf |
| EXCEL | .xlsx | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet |
| WORD | .docx | application/vnd.openxmlformats-officedocument.wordprocessingml.document |
| CSV | .csv | text/csv |
| XML | .xml | application/xml |
| IMAGE | .png | image/png |

#### Response

Binary content in the requested format with appropriate headers.

---

### Get Current User

Retrieve information about the authenticated user.

```http
GET /api/Reports/user
```

#### Response

```json
{
  "isAuthenticated": true,
  "name": "DOMAIN\\username",
  "authenticationType": "Negotiate",
  "isWindowsIdentity": true
}
```

---

### Search Reports and Folders

Recursively search for reports and folders by name or description.

```http
GET /api/Reports/search?query={searchTerm}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| query | string | Yes | Search term (case-insensitive, matches name or description) |

#### Response

```json
[
  {
    "type": "Report",
    "name": "Monthly Sales",
    "path": "/Sales/Monthly Sales",
    "description": "Monthly sales report",
    "createdDate": "2024-01-01T00:00:00Z",
    "modifiedDate": "2024-01-10T15:30:00Z"
  },
  {
    "type": "Folder",
    "name": "Regional",
    "path": "/Sales/Regional",
    "description": "Regional reports folder",
    "createdDate": "2024-01-01T00:00:00Z",
    "modifiedDate": "2024-01-10T15:30:00Z"
  }
]
```

---

## Management Controller Endpoints

Base path: `/api/Management`

### Create Folder

Create a new folder in SSRS.

```http
POST /api/Management/folder?parentPath={parentPath}&folderName={folderName}&description={description}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| parentPath | string | Yes | Path of the parent folder |
| folderName | string | Yes | Name of the new folder |
| description | string | No | Description of the folder |

#### Response

```json
{
  "message": "Folder created successfully"
}
```

---

### Delete Folder

Delete a folder in SSRS.

```http
DELETE /api/Management/folder?folderPath={folderPath}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| folderPath | string | Yes | Path of the folder to delete |

#### Response

```json
{
  "message": "Folder deleted successfully"
}
```

---

### Create Report

Create a new report in SSRS.

```http
POST /api/Management/report?parentPath={parentPath}&reportName={reportName}&description={description}
Content-Type: application/json

[report definition as byte array]
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| parentPath | string | Yes | Path of the parent folder |
| reportName | string | Yes | Name of the new report |
| description | string | No | Description of the report |

#### Request Body

Report definition (RDL) as byte array in JSON body.

#### Response

```json
{
  "message": "Report created successfully"
}
```

---

### Delete Report

Delete a report in SSRS.

```http
DELETE /api/Management/report?reportPath={reportPath}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| reportPath | string | Yes | Path of the report to delete |

#### Response

```json
{
  "message": "Report deleted successfully"
}
```

---

### Move Item

Move a report or folder to a new location in SSRS.

```http
POST /api/Management/move?itemPath={itemPath}&targetPath={targetPath}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| itemPath | string | Yes | Path of the item to move |
| targetPath | string | Yes | New path for the item |

#### Response

```json
{
  "message": "Item moved successfully"
}
```

---

## Security Controller Endpoints

Base path: `/api/Security`

### Get Policies

List all policies for an item.

```http
GET /api/Security/policies?itemPath={itemPath}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| itemPath | string | Yes | Path of the item to get policies for |

#### Response

```json
[
  {
    "groupUserName": "DOMAIN\\Users",
    "roles": ["Browser", "Content Manager"]
  }
]
```

---

### Set Policies

Set all policies for an item.

```http
POST /api/Security/policies?itemPath={itemPath}
Content-Type: application/json

[
  {
    "groupUserName": "DOMAIN\\Users",
    "roles": ["Browser"]
  }
]
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| itemPath | string | Yes | Path of the item to set policies for |

#### Request Body

Array of PolicyInfo objects with groupUserName and roles.

#### Response

```json
{
  "message": "Policies updated successfully"
}
```

---

### List Roles

List available SSRS roles.

```http
GET /api/Security/roles
```

#### Response

```json
[
  {
    "name": "Browser",
    "description": "May view folders, reports and subscribe to reports."
  },
  {
    "name": "Content Manager",
    "description": "May manage content in the Report Server."
  }
]
```

---

### Get User/Group Policies

List all items where a user/group has permissions.

```http
GET /api/Security/policies/user?userOrGroup={userOrGroup}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| userOrGroup | string | Yes | User or group name to search for |

#### Response

```json
[
  {
    "itemPath": "/Sales/Monthly Report",
    "itemType": "Report",
    "roles": ["Browser", "Content Manager"]
  }
]
```

---

## Data Models

### RenderRequest

```json
{
  "reportPath": "string",
  "parameters": {
    "key": "value"
  }
}
```

### ReportInfo

```json
{
  "name": "string",
  "path": "string",
  "type": "string",
  "createdDate": "datetime",
  "modifiedDate": "datetime",
  "description": "string"
}
```

### ReportParameter

```json
{
  "name": "string",
  "type": "string",
  "nullable": "boolean",
  "allowBlank": "boolean",
  "multiValue": "boolean",
  "validValues": ["string"],
  "defaultValue": "string",
  "prompt": "string"
}
```

### FolderInfo

```json
{
  "name": "string",
  "path": "string",
  "createdDate": "datetime",
  "modifiedDate": "datetime",
  "description": "string"
}
```

### FolderContent

```json
{
  "currentPath": "string",
  "folders": [FolderInfo],
  "reports": [ReportInfo]
}
```

### PolicyInfo

```json
{
  "groupUserName": "string",
  "roles": ["string"]
}
```

### RoleInfo

```json
{
  "name": "string",
  "description": "string"
}
```

---

## Error Handling

The API uses custom SSRSException handling with the following error codes:

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| ItemNotFound | 404 | Report or folder not found |
| AccessDenied | 403 | Access denied to resource |
| InvalidParameter | 400 | Invalid parameter provided |
| ParameterTypeMismatch | 400 | Parameter type mismatch |
| AuthenticationFailed | 401 | Authentication failed |
| UnexpectedError | 500 | Unexpected server error |

---

## CORS Configuration

The API is configured with CORS support for frontend applications:

- Allowed Origins: `http://localhost:5173` (development)
- Credentials: Allowed
- Headers: All allowed
- Methods: All allowed

---

## Best Practices

### Parameter Handling

1. **Date Parameters**: Use ISO 8601 format (`2024-01-15T10:30:00Z`)
2. **Multi-value Parameters**: Pass as arrays `["value1", "value2"]`
3. **Boolean Parameters**: Use `true`/`false` (lowercase)
4. **Null Parameters**: Omit from request or pass `null`

### Error Handling

```javascript
try {
  const response = await fetch('/api/Reports/render', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 
      reportPath: '/MyReport',
      parameters: { StartDate: '2024-01-01' }
    })
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(`${error.errorCode}: ${error.message}`);
  }

  return await response.blob();
} catch (error) {
  console.error('Report rendering failed:', error);
}
```

### Complete Workflow Example

```javascript
// 1. Test connection
const status = await fetch('/api/Reports/test-connection', {
  credentials: 'include'
}).then(r => r.json());

// 2. Browse available reports
const reports = await fetch('/api/Reports/browse', {
  credentials: 'include'
}).then(r => r.json());

// 3. Get parameters for specific report
const params = await fetch('/api/Reports/parameters?reportPath=/MyReport', {
  credentials: 'include'
}).then(r => r.json());

// 4. Render report
const pdf = await fetch('/api/Reports/render', {
  method: 'POST',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    reportPath: '/MyReport',
    parameters: { StartDate: '2024-01-01' }
  })
}).then(r => r.blob());

// 5. Search for reports
const searchResults = await fetch('/api/Reports/search?query=sales', {
  credentials: 'include'
}).then(r => r.json());
```

---

## Security Considerations

1. **Authentication**: Windows Authentication is required for all endpoints
2. **Authorization**: SSRS permissions are enforced at the server level
3. **HTTPS**: Use HTTPS in production environments
4. **Input Validation**: All parameters are validated before sending to SSRS
5. **Error Handling**: Sensitive information is not exposed in error messages
6. **Pass-through Authentication**: User credentials are passed through to SSRS maintaining security context