using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SideSpins.Api.Observations;

public class BlobService
{
    private readonly string _storageAccountName;
    private readonly string _containerName;
    private readonly string? _connectionString;

    public BlobService(string storageAccountName, string containerName)
    {
        _storageAccountName = storageAccountName;
        _containerName = containerName;
    }

    public BlobService(string connectionString, string storageAccountName, string containerName)
    {
        _connectionString = connectionString;
        _storageAccountName = storageAccountName;
        _containerName = containerName;
    }

    /// <summary>
    /// Generates a public blob URL for video playback.
    /// Assumes the container is publicly accessible (Option A - MVP approach).
    /// </summary>
    public string GetPublicBlobUrl(string blobName)
    {
        // Public blob URL format: https://{account}.blob.core.windows.net/{container}/{blob}
        return $"https://{_storageAccountName}.blob.core.windows.net/{_containerName}/{blobName}";
    }

    /// <summary>
    /// Gets the public URL from a RecordingPart object.
    /// Uses the container and blob name from the recording part.
    /// </summary>
    public string GetPublicBlobUrl(RecordingPart recordingPart)
    {
        var account = recordingPart.StorageAccount ?? _storageAccountName;
        return $"https://{account}.blob.core.windows.net/{recordingPart.Container}/{recordingPart.BlobName}";
    }

    /// <summary>
    /// Lists all blobs in the specified container.
    /// Returns blob names, sizes, and last modified timestamps.
    /// </summary>
    public async Task<List<BlobInfo>> ListBlobsAsync(string containerName)
    {
        var blobInfos = new List<BlobInfo>();

        try
        {
            BlobContainerClient containerClient;
            
            if (!string.IsNullOrEmpty(_connectionString))
            {
                // Use connection string (for local development with Azurite)
                containerClient = new BlobContainerClient(_connectionString, containerName);
            }
            else
            {
                // Use account name (for production with public access)
                var serviceClient = new BlobServiceClient(new Uri($"https://{_storageAccountName}.blob.core.windows.net"));
                containerClient = serviceClient.GetBlobContainerClient(containerName);
            }

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                // Filter for video files only
                var extension = Path.GetExtension(blobItem.Name).ToLowerInvariant();
                if (extension == ".mp4" || extension == ".mov" || extension == ".avi")
                {
                    blobInfos.Add(new BlobInfo
                    {
                        Name = blobItem.Name,
                        SizeBytes = blobItem.Properties.ContentLength ?? 0,
                        LastModified = blobItem.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue
                    });
                }
            }

            // Sort by name (which sorts by timestamp if using naming convention)
            return blobInfos.OrderBy(b => b.Name).ToList();
        }
        catch (Exception ex)
        {
            // Log error and return empty list
            Console.WriteLine($"Error listing blobs in container {containerName}: {ex.Message}");
            return blobInfos;
        }
    }
}

public class BlobInfo
{
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}
