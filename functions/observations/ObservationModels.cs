using Newtonsoft.Json;

namespace SideSpins.Api.Observations;

public class Observation
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "observation";

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty; // "practice" or "match"

    [JsonProperty("startTime")]
    public DateTime StartTime { get; set; }

    [JsonProperty("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "active"; // "active" or "completed"

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("recordingRef")]
    public RecordingRef? RecordingRef { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class RecordingRef
{
    [JsonProperty("provider")]
    public string Provider { get; set; } = "azure_blob";

    [JsonProperty("storageAccount")]
    public string? StorageAccount { get; set; }

    [JsonProperty("container")]
    public string Container { get; set; } = string.Empty;

    [JsonProperty("blobName")]
    public string BlobName { get; set; } = string.Empty;

    [JsonProperty("contentType")]
    public string ContentType { get; set; } = "video/mp4";

    [JsonProperty("recordingStartOffsetSeconds")]
    public int RecordingStartOffsetSeconds { get; set; } = 0;
}

public class Note
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "note";

    [JsonProperty("observationId")]
    public string ObservationId { get; set; } = string.Empty;

    [JsonProperty("offsetSeconds")]
    public int? OffsetSeconds { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
