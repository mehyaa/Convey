---
layout: default
title: Convey.Persistence.OpenStack.OCS
parent: Persistence
---
# Convey.Persistence.OpenStack.OCS

OpenStack Object Storage Service (Swift) integration providing scalable cloud object storage with authentication, containers management, and high-performance file operations for distributed applications.

## Installation

```bash
dotnet add package Convey.Persistence.OpenStack.OCS
```

## Overview

Convey.Persistence.OpenStack.OCS provides:
- **Object storage** - Store and retrieve files, documents, and binary data
- **Container management** - Organize objects in containers with metadata
- **Authentication** - OpenStack Keystone authentication integration
- **Large objects** - Support for large file uploads with segmentation
- **Metadata support** - Custom metadata for objects and containers
- **CDN integration** - Content delivery network support
- **Access control** - Fine-grained access control and permissions
- **Streaming operations** - Efficient streaming for large files

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddOpenStackOCS(ocs =>
    {
        ocs.AuthUrl = "https://auth.openstack.example.com:5000/v3";
        ocs.Username = "your-username";
        ocs.Password = "your-password";
        ocs.TenantName = "your-tenant";
        ocs.Region = "RegionOne";
        ocs.ContainerName = "default-container";
    });

var app = builder.Build();
app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddOpenStackOCS(ocs =>
    {
        ocs.AuthUrl = "https://auth.openstack.example.com:5000/v3";
        ocs.Username = Environment.GetEnvironmentVariable("OPENSTACK_USERNAME");
        ocs.Password = Environment.GetEnvironmentVariable("OPENSTACK_PASSWORD");
        ocs.TenantName = Environment.GetEnvironmentVariable("OPENSTACK_TENANT");
        ocs.TenantId = Environment.GetEnvironmentVariable("OPENSTACK_TENANT_ID");
        ocs.Region = Environment.GetEnvironmentVariable("OPENSTACK_REGION") ?? "RegionOne";
        ocs.ContainerName = "application-storage";
        ocs.UseServiceNet = false;
        ocs.DefaultHeaders = new Dictionary<string, string>
        {
            ["X-Custom-Header"] = "custom-value"
        };
        ocs.ChunkSize = 1024 * 1024 * 10; // 10MB chunks for large objects
        ocs.ConnectionTimeout = TimeSpan.FromSeconds(30);
        ocs.ReadWriteTimeout = TimeSpan.FromMinutes(5);
        ocs.RetryAttempts = 3;
        ocs.RetryDelay = TimeSpan.FromSeconds(2);
    });

var app = builder.Build();
app.Run();
```

### Multiple Container Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddOpenStackOCS("primary", ocs =>
    {
        ocs.AuthUrl = "https://auth.openstack.example.com:5000/v3";
        ocs.Username = "primary-user";
        ocs.Password = "primary-password";
        ocs.TenantName = "primary-tenant";
        ocs.Region = "RegionOne";
        ocs.ContainerName = "primary-storage";
    })
    .AddOpenStackOCS("backup", ocs =>
    {
        ocs.AuthUrl = "https://backup-auth.openstack.example.com:5000/v3";
        ocs.Username = "backup-user";
        ocs.Password = "backup-password";
        ocs.TenantName = "backup-tenant";
        ocs.Region = "RegionTwo";
        ocs.ContainerName = "backup-storage";
    });

var app = builder.Build();
app.Run();
```

## Key Features

### 1. File Upload and Download

Basic file operations with OpenStack Object Storage:

```csharp
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IOpenStackObjectStorage _objectStorage;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IOpenStackObjectStorage objectStorage, ILogger<FilesController> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string? path = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        try
        {
            var objectName = path != null ? $"{path}/{file.FileName}" : file.FileName;

            using var stream = file.OpenReadStream();
            var metadata = new Dictionary<string, string>
            {
                ["original-filename"] = file.FileName,
                ["content-type"] = file.ContentType,
                ["upload-date"] = DateTime.UtcNow.ToString("O"),
                ["file-size"] = file.Length.ToString(),
                ["uploader"] = User.Identity?.Name ?? "anonymous"
            };

            var result = await _objectStorage.CreateObjectAsync(objectName, stream, metadata);

            return Ok(new
            {
                ObjectName = objectName,
                Size = file.Length,
                ContentType = file.ContentType,
                ETag = result.ETag,
                Url = result.PublicUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            return StatusCode(500, "Error uploading file");
        }
    }

    [HttpPost("upload/large")]
    public async Task<IActionResult> UploadLargeFile(IFormFile file, [FromQuery] string? path = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        try
        {
            var objectName = path != null ? $"{path}/{file.FileName}" : file.FileName;

            using var stream = file.OpenReadStream();
            var metadata = new Dictionary<string, string>
            {
                ["original-filename"] = file.FileName,
                ["content-type"] = file.ContentType,
                ["upload-date"] = DateTime.UtcNow.ToString("O"),
                ["file-size"] = file.Length.ToString(),
                ["uploader"] = User.Identity?.Name ?? "anonymous",
                ["upload-type"] = "large-object"
            };

            // Use segmented upload for large files
            var result = await _objectStorage.CreateLargeObjectAsync(objectName, stream, metadata);

            return Ok(new
            {
                ObjectName = objectName,
                Size = file.Length,
                ContentType = file.ContentType,
                ETag = result.ETag,
                Url = result.PublicUrl,
                SegmentCount = result.SegmentCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading large file {FileName}", file.FileName);
            return StatusCode(500, "Error uploading file");
        }
    }

    [HttpGet("download/{*objectName}")]
    public async Task<IActionResult> DownloadFile(string objectName)
    {
        try
        {
            var objectInfo = await _objectStorage.GetObjectInfoAsync(objectName);
            if (objectInfo == null)
            {
                return NotFound();
            }

            var stream = await _objectStorage.GetObjectAsync(objectName);
            var contentType = objectInfo.ContentType ?? "application/octet-stream";
            var fileName = objectInfo.Metadata.TryGetValue("original-filename", out var originalName)
                ? originalName
                : objectName;

            return File(stream, contentType, fileName);
        }
        catch (ObjectNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {ObjectName}", objectName);
            return StatusCode(500, "Error downloading file");
        }
    }

    [HttpGet("info/{*objectName}")]
    public async Task<IActionResult> GetFileInfo(string objectName)
    {
        try
        {
            var objectInfo = await _objectStorage.GetObjectInfoAsync(objectName);
            if (objectInfo == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                ObjectName = objectName,
                Size = objectInfo.ContentLength,
                ContentType = objectInfo.ContentType,
                ETag = objectInfo.ETag,
                LastModified = objectInfo.LastModified,
                Metadata = objectInfo.Metadata,
                PublicUrl = objectInfo.PublicUrl
            });
        }
        catch (ObjectNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info {ObjectName}", objectName);
            return StatusCode(500, "Error getting file information");
        }
    }

    [HttpDelete("{*objectName}")]
    public async Task<IActionResult> DeleteFile(string objectName)
    {
        try
        {
            var exists = await _objectStorage.ObjectExistsAsync(objectName);
            if (!exists)
            {
                return NotFound();
            }

            await _objectStorage.DeleteObjectAsync(objectName);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {ObjectName}", objectName);
            return StatusCode(500, "Error deleting file");
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListFiles([FromQuery] string? prefix = null,
        [FromQuery] int limit = 100, [FromQuery] string? marker = null)
    {
        try
        {
            var objects = await _objectStorage.ListObjectsAsync(prefix, limit, marker);

            var response = new
            {
                Objects = objects.Select(obj => new
                {
                    Name = obj.Name,
                    Size = obj.ContentLength,
                    ContentType = obj.ContentType,
                    ETag = obj.ETag,
                    LastModified = obj.LastModified,
                    PublicUrl = obj.PublicUrl
                }),
                Count = objects.Count(),
                HasMore = objects.Count() == limit
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files");
            return StatusCode(500, "Error listing files");
        }
    }
}
```

### 2. Container Management

Managing containers and their metadata:

```csharp
public interface IContainerService
{
    Task CreateContainerAsync(string containerName, Dictionary<string, string>? metadata = null);
    Task DeleteContainerAsync(string containerName);
    Task<ContainerInfo> GetContainerInfoAsync(string containerName);
    Task<IEnumerable<ContainerInfo>> ListContainersAsync();
    Task UpdateContainerMetadataAsync(string containerName, Dictionary<string, string> metadata);
    Task<bool> ContainerExistsAsync(string containerName);
}

public class ContainerService : IContainerService
{
    private readonly IOpenStackObjectStorage _objectStorage;
    private readonly ILogger<ContainerService> _logger;

    public ContainerService(IOpenStackObjectStorage objectStorage, ILogger<ContainerService> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task CreateContainerAsync(string containerName, Dictionary<string, string>? metadata = null)
    {
        try
        {
            await _objectStorage.CreateContainerAsync(containerName, metadata ?? new Dictionary<string, string>());
            _logger.LogInformation("Container {ContainerName} created successfully", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task DeleteContainerAsync(string containerName)
    {
        try
        {
            // Ensure container is empty before deletion
            var objects = await _objectStorage.ListObjectsAsync(containerName: containerName);
            if (objects.Any())
            {
                throw new InvalidOperationException($"Container {containerName} is not empty");
            }

            await _objectStorage.DeleteContainerAsync(containerName);
            _logger.LogInformation("Container {ContainerName} deleted successfully", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task<ContainerInfo> GetContainerInfoAsync(string containerName)
    {
        try
        {
            return await _objectStorage.GetContainerInfoAsync(containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container info {ContainerName}", containerName);
            throw;
        }
    }

    public async Task<IEnumerable<ContainerInfo>> ListContainersAsync()
    {
        try
        {
            return await _objectStorage.ListContainersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing containers");
            throw;
        }
    }

    public async Task UpdateContainerMetadataAsync(string containerName, Dictionary<string, string> metadata)
    {
        try
        {
            await _objectStorage.UpdateContainerMetadataAsync(containerName, metadata);
            _logger.LogInformation("Container {ContainerName} metadata updated", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating container metadata {ContainerName}", containerName);
            throw;
        }
    }

    public async Task<bool> ContainerExistsAsync(string containerName)
    {
        try
        {
            return await _objectStorage.ContainerExistsAsync(containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking container existence {ContainerName}", containerName);
            throw;
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class ContainersController : ControllerBase
{
    private readonly IContainerService _containerService;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(IContainerService containerService, ILogger<ContainersController> logger)
    {
        _containerService = containerService;
        _logger = logger;
    }

    [HttpPost("{containerName}")]
    public async Task<IActionResult> CreateContainer(string containerName, [FromBody] CreateContainerRequest? request = null)
    {
        try
        {
            var metadata = request?.Metadata ?? new Dictionary<string, string>();

            // Add default metadata
            metadata["created-date"] = DateTime.UtcNow.ToString("O");
            metadata["created-by"] = User.Identity?.Name ?? "system";

            if (!string.IsNullOrEmpty(request?.Description))
            {
                metadata["description"] = request.Description;
            }

            await _containerService.CreateContainerAsync(containerName, metadata);
            return Created($"/api/containers/{containerName}", new { ContainerName = containerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating container {ContainerName}", containerName);
            return StatusCode(500, "Error creating container");
        }
    }

    [HttpDelete("{containerName}")]
    public async Task<IActionResult> DeleteContainer(string containerName)
    {
        try
        {
            var exists = await _containerService.ContainerExistsAsync(containerName);
            if (!exists)
            {
                return NotFound();
            }

            await _containerService.DeleteContainerAsync(containerName);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting container {ContainerName}", containerName);
            return StatusCode(500, "Error deleting container");
        }
    }

    [HttpGet("{containerName}")]
    public async Task<IActionResult> GetContainer(string containerName)
    {
        try
        {
            var containerInfo = await _containerService.GetContainerInfoAsync(containerName);
            return Ok(new
            {
                Name = containerInfo.Name,
                ObjectCount = containerInfo.ObjectCount,
                BytesUsed = containerInfo.BytesUsed,
                Metadata = containerInfo.Metadata,
                CreatedDate = containerInfo.Metadata.TryGetValue("created-date", out var createdDate)
                    ? DateTime.Parse(createdDate)
                    : (DateTime?)null,
                Description = containerInfo.Metadata.TryGetValue("description", out var description)
                    ? description
                    : null
            });
        }
        catch (ContainerNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container {ContainerName}", containerName);
            return StatusCode(500, "Error getting container information");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListContainers()
    {
        try
        {
            var containers = await _containerService.ListContainersAsync();

            var response = containers.Select(container => new
            {
                Name = container.Name,
                ObjectCount = container.ObjectCount,
                BytesUsed = container.BytesUsed,
                CreatedDate = container.Metadata.TryGetValue("created-date", out var createdDate)
                    ? DateTime.Parse(createdDate)
                    : (DateTime?)null,
                Description = container.Metadata.TryGetValue("description", out var description)
                    ? description
                    : null
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing containers");
            return StatusCode(500, "Error listing containers");
        }
    }

    [HttpPatch("{containerName}/metadata")]
    public async Task<IActionResult> UpdateContainerMetadata(string containerName, [FromBody] UpdateMetadataRequest request)
    {
        try
        {
            var exists = await _containerService.ContainerExistsAsync(containerName);
            if (!exists)
            {
                return NotFound();
            }

            // Add system metadata
            var metadata = request.Metadata ?? new Dictionary<string, string>();
            metadata["last-modified"] = DateTime.UtcNow.ToString("O");
            metadata["modified-by"] = User.Identity?.Name ?? "system";

            await _containerService.UpdateContainerMetadataAsync(containerName, metadata);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating container metadata {ContainerName}", containerName);
            return StatusCode(500, "Error updating container metadata");
        }
    }
}

public class CreateContainerRequest
{
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class UpdateMetadataRequest
{
    public Dictionary<string, string>? Metadata { get; set; }
}
```

### 3. Advanced File Operations

Advanced file operations including streaming and batch operations:

```csharp
public interface IAdvancedFileService
{
    Task<string> UploadFromStreamAsync(Stream stream, string objectName, string contentType, Dictionary<string, string>? metadata = null);
    Task<Stream> DownloadAsStreamAsync(string objectName);
    Task CopyObjectAsync(string sourceObjectName, string destinationObjectName, string? destinationContainer = null);
    Task<IEnumerable<string>> BatchUploadAsync(IEnumerable<(Stream Stream, string ObjectName, string ContentType)> files);
    Task BatchDeleteAsync(IEnumerable<string> objectNames);
    Task<string> CreateTemporaryUrlAsync(string objectName, TimeSpan expiration, string method = "GET");
}

public class AdvancedFileService : IAdvancedFileService
{
    private readonly IOpenStackObjectStorage _objectStorage;
    private readonly ILogger<AdvancedFileService> _logger;

    public AdvancedFileService(IOpenStackObjectStorage objectStorage, ILogger<AdvancedFileService> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task<string> UploadFromStreamAsync(Stream stream, string objectName, string contentType, Dictionary<string, string>? metadata = null)
    {
        try
        {
            var uploadMetadata = metadata ?? new Dictionary<string, string>();
            uploadMetadata["upload-date"] = DateTime.UtcNow.ToString("O");
            uploadMetadata["content-type"] = contentType;
            uploadMetadata["stream-upload"] = "true";

            var result = await _objectStorage.CreateObjectAsync(objectName, stream, uploadMetadata);
            _logger.LogInformation("Stream uploaded successfully to {ObjectName}", objectName);

            return result.ETag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading stream to {ObjectName}", objectName);
            throw;
        }
    }

    public async Task<Stream> DownloadAsStreamAsync(string objectName)
    {
        try
        {
            var stream = await _objectStorage.GetObjectAsync(objectName);
            _logger.LogInformation("Stream downloaded successfully from {ObjectName}", objectName);

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading stream from {ObjectName}", objectName);
            throw;
        }
    }

    public async Task CopyObjectAsync(string sourceObjectName, string destinationObjectName, string? destinationContainer = null)
    {
        try
        {
            await _objectStorage.CopyObjectAsync(sourceObjectName, destinationObjectName, destinationContainer);
            _logger.LogInformation("Object copied from {Source} to {Destination}", sourceObjectName, destinationObjectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying object from {Source} to {Destination}", sourceObjectName, destinationObjectName);
            throw;
        }
    }

    public async Task<IEnumerable<string>> BatchUploadAsync(IEnumerable<(Stream Stream, string ObjectName, string ContentType)> files)
    {
        var uploadTasks = files.Select(async file =>
        {
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    ["upload-date"] = DateTime.UtcNow.ToString("O"),
                    ["content-type"] = file.ContentType,
                    ["batch-upload"] = "true"
                };

                var result = await _objectStorage.CreateObjectAsync(file.ObjectName, file.Stream, metadata);
                _logger.LogInformation("Batch upload successful for {ObjectName}", file.ObjectName);

                return result.ETag;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch upload for {ObjectName}", file.ObjectName);
                throw;
            }
        });

        var results = await Task.WhenAll(uploadTasks);
        return results;
    }

    public async Task BatchDeleteAsync(IEnumerable<string> objectNames)
    {
        var deleteTasks = objectNames.Select(async objectName =>
        {
            try
            {
                await _objectStorage.DeleteObjectAsync(objectName);
                _logger.LogInformation("Batch delete successful for {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch delete for {ObjectName}", objectName);
                throw;
            }
        });

        await Task.WhenAll(deleteTasks);
    }

    public async Task<string> CreateTemporaryUrlAsync(string objectName, TimeSpan expiration, string method = "GET")
    {
        try
        {
            var tempUrl = await _objectStorage.CreateTemporaryUrlAsync(objectName, expiration, method);
            _logger.LogInformation("Temporary URL created for {ObjectName}, expires in {Expiration}", objectName, expiration);

            return tempUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating temporary URL for {ObjectName}", objectName);
            throw;
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class AdvancedFilesController : ControllerBase
{
    private readonly IAdvancedFileService _advancedFileService;
    private readonly ILogger<AdvancedFilesController> _logger;

    public AdvancedFilesController(IAdvancedFileService advancedFileService, ILogger<AdvancedFilesController> logger)
    {
        _advancedFileService = advancedFileService;
        _logger = logger;
    }

    [HttpPost("stream/{objectName}")]
    public async Task<IActionResult> UploadStream(string objectName, [FromQuery] string contentType = "application/octet-stream")
    {
        try
        {
            using var stream = Request.Body;
            var etag = await _advancedFileService.UploadFromStreamAsync(stream, objectName, contentType);

            return Ok(new { ObjectName = objectName, ETag = etag });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading stream to {ObjectName}", objectName);
            return StatusCode(500, "Error uploading stream");
        }
    }

    [HttpGet("stream/{objectName}")]
    public async Task<IActionResult> DownloadStream(string objectName)
    {
        try
        {
            var stream = await _advancedFileService.DownloadAsStreamAsync(objectName);
            return File(stream, "application/octet-stream", objectName);
        }
        catch (ObjectNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading stream from {ObjectName}", objectName);
            return StatusCode(500, "Error downloading stream");
        }
    }

    [HttpPost("copy")]
    public async Task<IActionResult> CopyObject([FromBody] CopyObjectRequest request)
    {
        try
        {
            await _advancedFileService.CopyObjectAsync(request.SourceObjectName, request.DestinationObjectName, request.DestinationContainer);
            return Ok(new { Message = "Object copied successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying object");
            return StatusCode(500, "Error copying object");
        }
    }

    [HttpPost("batch-upload")]
    public async Task<IActionResult> BatchUpload(IEnumerable<IFormFile> files)
    {
        try
        {
            var fileStreams = files.Select(file => (
                Stream: file.OpenReadStream(),
                ObjectName: file.FileName,
                ContentType: file.ContentType
            ));

            var etags = await _advancedFileService.BatchUploadAsync(fileStreams);

            return Ok(new { Message = "Batch upload completed", ETags = etags });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch upload");
            return StatusCode(500, "Error in batch upload");
        }
    }

    [HttpDelete("batch")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest request)
    {
        try
        {
            await _advancedFileService.BatchDeleteAsync(request.ObjectNames);
            return Ok(new { Message = "Batch delete completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch delete");
            return StatusCode(500, "Error in batch delete");
        }
    }

    [HttpPost("temp-url/{objectName}")]
    public async Task<IActionResult> CreateTemporaryUrl(string objectName, [FromBody] TemporaryUrlRequest request)
    {
        try
        {
            var tempUrl = await _advancedFileService.CreateTemporaryUrlAsync(objectName, request.Expiration, request.Method);

            return Ok(new
            {
                TemporaryUrl = tempUrl,
                ExpiresAt = DateTime.UtcNow.Add(request.Expiration),
                Method = request.Method
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating temporary URL for {ObjectName}", objectName);
            return StatusCode(500, "Error creating temporary URL");
        }
    }
}

public class CopyObjectRequest
{
    public string SourceObjectName { get; set; } = string.Empty;
    public string DestinationObjectName { get; set; } = string.Empty;
    public string? DestinationContainer { get; set; }
}

public class BatchDeleteRequest
{
    public IEnumerable<string> ObjectNames { get; set; } = Enumerable.Empty<string>();
}

public class TemporaryUrlRequest
{
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);
    public string Method { get; set; } = "GET";
}
```

### 4. CDN Integration

Content Delivery Network integration for improved performance:

```csharp
public interface ICdnService
{
    Task EnableCdnAsync(string containerName, int ttl = 3600);
    Task DisableCdnAsync(string containerName);
    Task<CdnInfo> GetCdnInfoAsync(string containerName);
    Task UpdateCdnSettingsAsync(string containerName, CdnSettings settings);
    Task PurgeCdnCacheAsync(string containerName, string? objectName = null);
}

public class CdnService : ICdnService
{
    private readonly IOpenStackObjectStorage _objectStorage;
    private readonly ILogger<CdnService> _logger;

    public CdnService(IOpenStackObjectStorage objectStorage, ILogger<CdnService> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task EnableCdnAsync(string containerName, int ttl = 3600)
    {
        try
        {
            await _objectStorage.EnableCdnAsync(containerName, ttl);
            _logger.LogInformation("CDN enabled for container {ContainerName} with TTL {TTL}", containerName, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling CDN for container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task DisableCdnAsync(string containerName)
    {
        try
        {
            await _objectStorage.DisableCdnAsync(containerName);
            _logger.LogInformation("CDN disabled for container {ContainerName}", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling CDN for container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task<CdnInfo> GetCdnInfoAsync(string containerName)
    {
        try
        {
            return await _objectStorage.GetCdnInfoAsync(containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CDN info for container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task UpdateCdnSettingsAsync(string containerName, CdnSettings settings)
    {
        try
        {
            await _objectStorage.UpdateCdnSettingsAsync(containerName, settings);
            _logger.LogInformation("CDN settings updated for container {ContainerName}", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CDN settings for container {ContainerName}", containerName);
            throw;
        }
    }

    public async Task PurgeCdnCacheAsync(string containerName, string? objectName = null)
    {
        try
        {
            await _objectStorage.PurgeCdnCacheAsync(containerName, objectName);
            var target = objectName != null ? $"object {objectName}" : "entire container";
            _logger.LogInformation("CDN cache purged for {Target} in container {ContainerName}", target, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging CDN cache for container {ContainerName}", containerName);
            throw;
        }
    }
}

public class CdnSettings
{
    public int TTL { get; set; } = 3600;
    public bool Enabled { get; set; } = true;
    public string? LogRetention { get; set; }
    public bool CorsEnabled { get; set; } = false;
    public IEnumerable<string>? AllowedOrigins { get; set; }
}

public class CdnInfo
{
    public bool Enabled { get; set; }
    public string? CdnUrl { get; set; }
    public string? SslUrl { get; set; }
    public int TTL { get; set; }
    public string? LogRetention { get; set; }
    public bool CorsEnabled { get; set; }
}
```

## Configuration Options

### OpenStack OCS Options

```csharp
public class OpenStackOcsOptions
{
    public string AuthUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string Region { get; set; } = "RegionOne";
    public string ContainerName { get; set; } = "default";
    public bool UseServiceNet { get; set; } = false;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    public int ChunkSize { get; set; } = 1024 * 1024 * 5; // 5MB
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ReadWriteTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddOpenStackOCS(this IConveyBuilder builder);
    public static IConveyBuilder AddOpenStackOCS(this IConveyBuilder builder, Action<OpenStackOcsOptions> configure);
    public static IConveyBuilder AddOpenStackOCS(this IConveyBuilder builder, string sectionName);
    public static IConveyBuilder AddOpenStackOCS(this IConveyBuilder builder, string name, Action<OpenStackOcsOptions> configure);
}
```

### Core Interfaces

```csharp
public interface IOpenStackObjectStorage
{
    Task<ObjectResult> CreateObjectAsync(string objectName, Stream content, Dictionary<string, string>? metadata = null);
    Task<ObjectResult> CreateLargeObjectAsync(string objectName, Stream content, Dictionary<string, string>? metadata = null);
    Task<Stream> GetObjectAsync(string objectName);
    Task<ObjectInfo> GetObjectInfoAsync(string objectName);
    Task DeleteObjectAsync(string objectName);
    Task<bool> ObjectExistsAsync(string objectName);
    Task<IEnumerable<ObjectInfo>> ListObjectsAsync(string? prefix = null, int limit = 100, string? marker = null, string? containerName = null);
    Task CopyObjectAsync(string sourceObjectName, string destinationObjectName, string? destinationContainer = null);
    Task<string> CreateTemporaryUrlAsync(string objectName, TimeSpan expiration, string method = "GET");
}
```

## Best Practices

1. **Use appropriate chunk sizes** - Configure chunk sizes based on network conditions
2. **Implement retry logic** - Handle transient failures with exponential backoff
3. **Secure credentials** - Store authentication credentials securely
4. **Monitor storage usage** - Track storage consumption and costs
5. **Use metadata effectively** - Store relevant metadata for efficient querying
6. **Implement caching** - Cache frequently accessed objects locally
7. **Use CDN for public content** - Enable CDN for better performance
8. **Implement proper error handling** - Handle various OpenStack error scenarios

## Troubleshooting

### Common Issues

1. **Authentication failures**
   - Verify credentials and tenant information
   - Check Keystone endpoint availability
   - Ensure proper region configuration

2. **Large file upload issues**
   - Use segmented uploads for large files
   - Configure appropriate timeouts
   - Monitor network stability

3. **Performance issues**
   - Optimize chunk sizes for your environment
   - Use concurrent uploads when possible
   - Enable CDN for frequently accessed content

4. **Storage quota exceeded**
   - Monitor storage usage regularly
   - Implement cleanup policies for old files
   - Consider lifecycle management policies

