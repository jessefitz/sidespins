using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SideSpins.Api.Observations;

public class BlobService
{
    private readonly string _storageAccountName;
    private readonly string _containerName;

    public BlobService(string storageAccountName, string containerName)
    {
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
    /// Gets the public URL from a RecordingRef object.
    /// Uses the container and blob name from the recording reference.
    /// </summary>
    public string GetPublicBlobUrl(RecordingRef recordingRef)
    {
        var account = recordingRef.StorageAccount ?? _storageAccountName;
        return $"https://{account}.blob.core.windows.net/{recordingRef.Container}/{recordingRef.BlobName}";
    }
}
