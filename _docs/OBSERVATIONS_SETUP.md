# Observations Feature - Setup Guide

## Backend Implementation Complete ✅

The observations feature backend is now fully implemented with:

### Backend Components:
1. **Data Models** (`functions/observations/ObservationModels.cs`):
   - `Observation` - Main observation entity (practice/match sessions)
   - `Note` - Timestamped or general notes
   - `RecordingRef` - Video blob reference metadata

2. **Services** (`functions/observations/`):
   - `ObservationsService.cs` - Cosmos DB CRUD operations
   - `BlobService.cs` - Public blob URL generation

3. **API Endpoints**:
   - `ObservationsFunctions.cs`:
     - `GET /api/GetObservations` - List all observations
     - `GET /api/observations/{id}` - Get observation details
     - `POST /api/CreateObservation` - Create new observation
     - `PATCH /api/observations/{id}` - Update observation
     - `DELETE /api/observations/{id}` - Delete observation
     - `GET /api/observations/{id}/video-url` - Get video playback URL

   - `NotesFunctions.cs`:
     - `GET /api/observations/{observationId}/notes` - List notes
     - `GET /api/observations/{observationId}/notes/{id}` - Get note
     - `POST /api/observations/{observationId}/notes` - Create note
     - `PATCH /api/observations/{observationId}/notes/{id}` - Update note
     - `DELETE /api/observations/{observationId}/notes/{id}` - Delete note

### Frontend:
- `docs/observations.html` - Full-featured observation manager with:
  - Observation creation and management
  - Video player with HTML5 controls
  - Note-based seeking (click timestamp to jump to video position)
  - Attach recording form
  - Real-time note adding with current video time

---

## Setup Steps

### 1. Create Cosmos DB Containers

Run the Python import script to create the new containers:

```powershell
cd db
$env:COSMOS_URI = "https://jessefitzcosmos-dev.documents.azure.com:443/"

python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db
```

This will create two new containers:
- **Observations** (partition key: `/id`)
- **Notes** (partition key: `/observationId`)

### 2. Create Azure Blob Storage Container

#### Option A: Using Azure Portal
1. Go to your Azure Storage account
2. Navigate to "Containers"
3. Create a new container named `observations-videos`
4. Set public access level to "Blob" (allows public read access)

#### Option B: Using Azure CLI
```bash
az storage container create \
  --name observations-videos \
  --account-name <your-storage-account-name> \
  --public-access blob
```

#### Option C: For local development with Azurite
```powershell
# Install Azure Storage Explorer or use az CLI
az storage container create \
  --name observations-videos \
  --connection-string "UseDevelopmentStorage=true" \
  --public-access blob
```

### 3. Build and Run Backend

```powershell
cd functions
dotnet build
func start
```

Backend will run on `http://localhost:7071`

### 4. Run Frontend

```powershell
cd docs
bundle exec jekyll serve
```

Frontend will run on `http://localhost:4000`

Visit: `http://localhost:4000/observations.html`

---

## Testing the Feature

### 1. Create an Observation
1. Click "New Observation"
2. Select type (Practice or Match)
3. Add optional description
4. Click "Start Observation"

### 2. Manually Upload Videos

**Recommended: Use the video-processing.ps1 script** for automated processing, renaming, and upload:

```powershell
cd Tools\PoolIngest

# Process videos from a dated folder (recommended)
.\video-processing.ps1 -SourceDirectory "2026-01-28"

# Or process directly from USB drive
.\video-processing.ps1 -DriveLetter E
```

The script will:
1. Extract video metadata (creation_time, duration) using ffprobe
2. Rename files with UTC timestamps: `YYYYMMDD_HHmmss_D{duration}_{seq}_{name}.mp4`
3. Upload to Azure Blob Storage via azcopy

**Alternative: Manual upload** via Azure Storage Explorer, Azure Portal, or azcopy:
- Upload MP4 files to the `observations-videos` container
- Use the new naming convention: `YYYYMMDD_HHmmss_D{duration}_{seq}_{originalname}.mp4`
  - Example: `20260129_004011_D211_001_MVI_0066.MP4`
  - `20260129_004011` = UTC creation time (extracted via ffprobe)
  - `D211` = Duration in seconds
  - `001` = Sequence number for collision handling
  - `MVI_0066` = Original camera filename
- Run `ffmpeg -i input.mp4 -c copy -movflags faststart output.mp4` for web streaming optimization

**Legacy format still supported**: `YYYY_MMDD_HHMMSS_SEQ.mp4` (e.g., `2026_0109_143000_001.mp4`)

### 3. Attach Videos to Observation (New Blob Picker Workflow)
1. Open the observation
2. Click "Attach Recording"
3. Enter container name (default: `observations-videos`)
4. Click "Load Videos" - displays all available video files
5. **Select multiple videos** using checkboxes:
   - Use search box to filter files
   - Click "Select All" for bulk selection
   - System shows "X videos selected" count
6. Click "Add Selected Videos"
7. **Automatic processing**:
   - System parses filenames for timestamps and sequence numbers
   - Calculates start offsets based on observation start time
   - Calculates durations from consecutive file timestamps
   - Creates all recording parts with proper timeline alignment
8. **Alternative**: Click "Manual Entry" for non-standard filenames

### 4. Add Notes
1. Watch the video
2. Pause at interesting moments
3. Click "Use Current Video Time" to capture timestamp
4. Add note text
5. Click "Add Note"

### 5. Test Note-Based Seeking
1. Click any timestamped note
2. Video should jump to that moment and auto-play

---

## Configuration

### Production Environment Variables (Azure App Settings)

Add these to your Azure Functions App Configuration:

```
BLOB_STORAGE_ACCOUNT_NAME=<your-storage-account-name>
BLOB_CONTAINER_NAME=observations-videos
```

Update `local.settings.json` for local development (already done).

---

## Video Playback Requirements

### Public Blob Container (Current Implementation - Option A)
- ✅ Simplest setup for MVP
- ✅ No SAS token generation needed
- ✅ Direct blob URLs work immediately
- ⚠️ Videos are publicly accessible (anyone with URL can view)

### To Upgrade to Private Blobs Later:
1. Change container access level to "Private"
2. Update `BlobService.cs` to generate SAS tokens:
   ```csharp
   public string GetSasUrl(RecordingRef recordingRef)
   {
       var blobClient = new BlobClient(connectionString, container, blob);
       var sasBuilder = new BlobSasBuilder
       {
           BlobContainerName = container,
           BlobName = blob,
           Resource = "b",
           ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)
       };
       sasBuilder.SetPermissions(BlobSasPermissions.Read);
       var sasToken = blobClient.GenerateSasUri(sasBuilder);
       return sasToken.ToString();
   }
   ```
3. Update frontend to fetch SAS URL from backend before playing

---

## Troubleshooting

### Backend build fails
```powershell
cd functions
dotnet clean
dotnet restore
dotnet build
```

### Containers not created
- Verify Cosmos DB connection in `local.settings.json`
- Run Python import script with `--create-db` flag
- Check Azure Portal to verify containers exist

### Video doesn't play
- Verify blob exists in storage container
- Check browser console for CORS errors
- Ensure container has public access level set to "Blob"
- Test blob URL directly in browser

### Notes don't seek properly
- Verify `recordingStartOffsetSeconds` is set correctly
- Check browser console for JavaScript errors
- Ensure video has loaded before seeking

---

## Next Steps (Post-MVP)

### Immediate Priority

1. **Test Multi-Part Video Functionality**
   - Test filename parsing with new format `YYYYMMDD_HHmmss_D{duration}_{seq}_{name}.EXT`
   - Legacy format `YYYY_MMDD_HHMMSS_SEQ[_custom].EXT` also supported
   - Verify auto-calculation of part numbers, offsets, and durations
   - Test video auto-transition between parts
   - Verify note timestamp navigation across multiple parts (uses DateTime-based correlation)
   - Test edge cases (non-standard filenames, missing parts, etc.)

2. **Video Processing with video-processing.ps1 (RECOMMENDED)**
   
   **Why This Is Required**: HTML5 video players need the moov atom (metadata) at the beginning of MP4 files to enable seeking/scrubbing. Files from cameras typically have the moov atom at the end, which breaks browser playback.
   
   **Use the automated script** for processing and uploading:
   
   ```powershell
   cd Tools\PoolIngest
   
   # Process an existing dated folder (most common)
   .\video-processing.ps1 -SourceDirectory "2026-01-28"
   
   # Process from USB drive with ffmpeg conversion
   .\video-processing.ps1 -DriveLetter E
   
   # Preview what would happen without making changes
   .\video-processing.ps1 -SourceDirectory "2026-01-28" -WhatIf -SkipUpload
   ```
   
   The script extracts metadata using ffprobe and renames files with embedded timestamps:
   - `20260129_004011_D211_001_MVI_0066.MP4`
   - UTC creation time + duration + sequence + original name
   
   **Manual processing** (if not using the script):
   
   ```bash
   # Basic command - remuxes file with moov atom at start
   ffmpeg -i input.mp4 -c copy -movflags faststart output.mp4
   
   # Batch process multiple files (PowerShell)
   Get-ChildItem *.mp4 | ForEach-Object {
     ffmpeg -i $_.FullName -c copy -movflags faststart "remux_$($_.Name)"
   }
   
   # Batch process multiple files (Bash)
   for file in *.mp4; do
     ffmpeg -i "$file" -c copy -movflags faststart "remux_${file}"
   done
   ```
   
   **Important Notes**:
   - Use `-c copy` to avoid re-encoding (fast, lossless)
   - The `faststart` flag moves the moov atom to the beginning
   - Without this step, browser seeking will fail or be severely limited
   - Process files locally before uploading to Azure Blob Storage
   - Consider using `video-processing.ps1` script which handles this automatically

3. **Video Upload and Metadata Workflow**
   - The `video-processing.ps1` script automates the entire workflow
   - Extracts `creation_time` (UTC) and duration from video metadata using ffprobe
   - Renames files with embedded timestamps for timeline correlation
   - Uploads to Azure Blob Storage with retry logic
   - Frontend parses filenames to extract DateTime-based `startTime` and duration

### Future Enhancements

1. **Direct Upload from UI** - Add file picker and multipart upload
2. **SAS Token Security** - Move to private container with SAS tokens
3. **Video Thumbnails** - Generate preview thumbnails for observations list
4. **Live Timer** - Show elapsed time for active observations
5. **Export/Share** - Export observations with notes as PDF
6. **Team Filtering** - Filter observations by team/player
7. **Advanced Search** - Search notes content, date ranges

---

## Architecture Notes

### Why Public Blobs for MVP?
- Simplifies initial implementation
- No token refresh logic needed
- Direct video streaming from Azure CDN
- Easy to test and debug

### Partition Key Strategy
- **Observations** (`/id`): Self-partitioned for even distribution
- **Notes** (`/observationId`): Efficient queries per observation

### Video Format Support
- Primary: MP4 (H.264 video, AAC audio)
- Browser-native HTML5 video player
- No transcoding required for MVP

---

## API Testing

Test endpoints with PowerShell:

```powershell
$token = "your-jwt-token-here"
$baseUrl = "http://localhost:7071/api"

# Create observation
$body = @{
    label = "practice"
    description = "Evening practice session"
    startTime = (Get-Date).ToUniversalTime().ToString("o")
    status = "active"
} | ConvertTo-Json

Invoke-RestMethod -Uri "$baseUrl/CreateObservation" `
    -Method POST `
    -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" } `
    -Body $body

# List observations
Invoke-RestMethod -Uri "$baseUrl/GetObservations" `
    -Method GET `
    -Headers @{ "Authorization" = "Bearer $token" }
```

## CDN (Azure Front Door) key configuration

Usage of CDN eliminates the issue with the non-presence of the "Accepted-Ranges: bytes" header that was preventing browser playback from allong seeking to different parts of a video.  

### Front Door profile

* Azure Front Door (Standard)
* Default `*.azurefd.net` endpoint

### Origin configuration

* Origin type: Azure Blob Storage
* Origin host name: `jessefitzblob.blob.core.windows.net`
* Origin host header: `jessefitzblob.blob.core.windows.net`
* Protocol: HTTPS

### Origin group

* Single origin
* Session affinity: disabled
* Health probes:

  * Enabled
  * Method: **GET**
  * Probe path: real blob object (e.g. `/videos/<known-file>.mp4`)

### Route configuration

* Route enabled
* Domain: Front Door endpoint (`*.azurefd.net`)
* Patterns to match: `/videos/*`
* Accepted protocols: HTTPS only
* Forwarding protocol: HTTPS only
* Origin path: blank
* Redirect HTTP to HTTPS: enabled

### Media-specific outcome

* Range requests preserved and forwarded
* `Accept-Ranges: bytes` advertised to clients
* Browser video seeking enabled
