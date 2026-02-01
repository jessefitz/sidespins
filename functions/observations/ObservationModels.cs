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

    [JsonProperty("recordingParts")]
    public List<RecordingPart> RecordingParts { get; set; } = new List<RecordingPart>();

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class RecordingPart
{
    [JsonProperty("partNumber")]
    public int PartNumber { get; set; }

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

    /// <summary>
    /// UTC DateTime when this video recording started (extracted from video metadata via ffprobe).
    /// Used to correlate notes with specific moments in the video.
    /// </summary>
    [JsonProperty("startTime")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Duration of this video part in seconds (extracted from video metadata via ffprobe).
    /// </summary>
    [JsonProperty("durationSeconds")]
    public int? DurationSeconds { get; set; }

    // Legacy field for backward compatibility - will be removed in future version
    [JsonProperty("startOffsetSeconds")]
    public int StartOffsetSeconds { get; set; } = 0;
}

public class Note
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "note";

    [JsonProperty("observationId")]
    public string ObservationId { get; set; } = string.Empty;

    /// <summary>
    /// UTC DateTime when this note was recorded (the moment in time the note refers to).
    /// For moment notes, this is used to seek to the correct position in the video.
    /// For general notes, this is null.
    /// </summary>
    [JsonProperty("timestamp")]
    public DateTime? Timestamp { get; set; }

    // Legacy field for backward compatibility - will be removed in future version
    [JsonProperty("offsetSeconds")]
    public int? OffsetSeconds { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("createdByAuthUserId")]
    public string? CreatedByAuthUserId { get; set; }

    [JsonProperty("createdByName")]
    public string? CreatedByName { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
