# USB Video Ingest Utility

A PowerShell-based utility for ingesting raw video files from removable media, optimizing them for streaming with ffmpeg faststart, extracting video metadata using ffprobe, renaming files with UTC timestamps for timeline correlation, and uploading to Azure Blob Storage.

## Current Status: Version 1.3.0

**v1.3.0-metadata** implements:
- Phase 1: FFmpeg conversion with faststart for browser streaming
- Phase 2: FFprobe metadata extraction (creation time, duration) and smart file renaming
- Phase 3: Azure Blob Storage upload via azcopy

## Output Filename Format

Files are renamed with embedded metadata for correlation with the Observations feature:

```
YYYYMMDD_HHmmss_D{duration}_{seq}_{originalname}.mp4
```

**Example:** `20260129_004011_D211_001_MVI_0066.MP4`
- `20260129_004011` - UTC creation time from video metadata (Jan 29, 2026 at 00:40:11 UTC)
- `D211` - Duration in seconds (211 seconds = 3:31)
- `001` - Sequence number (for same-second collision handling)
- `MVI_0066` - Original camera filename

This format enables:
- **Automatic timeline ordering** - Videos sorted by exact recording start time
- **Precise note correlation** - Notes can be linked to absolute timestamps
- **Multi-part video support** - Frontend can calculate exact positions across video segments

## Prerequisites

### Required Software

1. **FFmpeg** - For video stream optimization
   - Install via [ffmpeg.org](https://ffmpeg.org/download.html) or `winget install ffmpeg`
   - Ensure `ffmpeg` is in your PATH, or configure `FfmpegPath` in `config.json`
   - Verify: `ffmpeg -version`

2. **FFprobe** - For video metadata extraction (usually bundled with ffmpeg)
   - Verify: `ffprobe -version`
   - Configure `FfprobePath` in `config.json` if not in PATH

3. **AzCopy** - For Azure Blob Storage upload (optional)
   - Install from [aka.ms/downloadazcopy](https://aka.ms/downloadazcopy)
   - Configure in `config.json` AzCopy section

### System Requirements

- Windows PowerShell 5.1+ or PowerShell 7+
- Write access to `%USERPROFILE%\Videos\pool`
- Write access to `%LOCALAPPDATA%\PoolIngest\logs`

## Quick Start

### Basic Usage

```powershell
# Process all MP4 files on drive D: (full pipeline: convert, rename, upload)
.\video-processing.ps1 -DriveLetter D

# Process existing folder (no ffmpeg conversion, just metadata + upload)
.\video-processing.ps1 -SourceDirectory "2026-01-28"

# Process folder with ffmpeg conversion
.\video-processing.ps1 -SourceDirectory "2026-01-28" -MovFlags

# Dry-run mode (shows what would be processed)
.\video-processing.ps1 -SourceDirectory "2026-01-28" -WhatIf

# Upload only (skip processing, use most recent folder)
.\video-processing.ps1 -UploadOnly

# Process without uploading
.\video-processing.ps1 -DriveLetter D -SkipUpload
```

### What It Does

1. **Phase 1 - Local Conversion** (optional with `-MovFlags`):
   - Validates the specified drive exists
   - Scans recursively for `*.mp4` files
   - Creates output folder: `%USERPROFILE%\Videos\pool\YYYY-MM-DD\`
   - Processes each video with ffmpeg (copies streams, relocates moov atom)

2. **Phase 2 - Metadata Extraction & Rename** (always runs):
   - Extracts `creation_time` UTC timestamp using ffprobe
   - Extracts video duration using ffprobe
   - Renames files with embedded metadata for timeline correlation
   - Handles same-second collisions with sequence numbers

3. **Phase 3 - Azure Upload** (unless `-SkipUpload`):
   - Uploads renamed files to Azure Blob Storage
   - Retry logic with up to 3 attempts
   - Skips files already present in destination

### Output Structure

```
C:\Users\<YourName>\Videos\pool\
  └── 2026-01-28\
      ├── 20260129_002208_D238_001_MVI_0064.MP4
      ├── 20260129_002708_D722_001_MVI_0065.MP4
      ├── 20260129_004011_D211_001_MVI_0066.MP4
      └── ...
```

### Log Files

Logs are stored in: `%LOCALAPPDATA%\PoolIngest\logs\ingest_YYYY-MM-DD_HHmmss.log`

Each log includes:
- Timestamp for each operation
- Metadata extraction results (creation time, duration)
- File rename operations
- Upload status with retry attempts
- Final summary statistics

## Configuration

Edit `config.json` to customize behavior:

```json
{
  "OutputRoot": "%USERPROFILE%\\Videos\\pool",
  "AppendSuffix": "_faststart",
  "FfmpegPath": "ffmpeg",
  "FfprobePath": "ffprobe",
  "IncludeTimestampInFolder": false,
  "FileExtensions": [".mp4"],
  "SkipIfExists": true,
  "ExcludePaths": [
    "System Volume Information",
    "$RECYCLE.BIN",
    "RECYCLER"
  ],
  "LogDirectory": "%LOCALAPPDATA%\\PoolIngest\\logs",
  "AzCopy": {
    "Enabled": true,
    "AzCopyPath": "azcopy",
    "BaseUrl": "https://yourstorage.blob.core.windows.net",
    "ContainerName": "videos",
    "RemotePrefix": "",
    "SasToken": "sv=...",
    "IncludePattern": "*.mp4;*.MP4",
    "OverwritePolicy": "ifSourceNewer"
  }
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `OutputRoot` | Base directory for processed videos | `%USERPROFILE%\Videos\pool` |
| `AppendSuffix` | Suffix added to filenames (ffmpeg conversion) | `_faststart` |
| `FfmpegPath` | Path to ffmpeg executable | `ffmpeg` (uses PATH) |
| `FfprobePath` | Path to ffprobe executable | `ffprobe` (uses PATH) |
| `IncludeTimestampInFolder` | Add HHmmss to date folder | `false` |
| `FileExtensions` | File types to process | `[".mp4"]` |
| `SkipIfExists` | Skip if output file exists | `true` |
| `ExcludePaths` | Folder names to skip | System folders |
| `LogDirectory` | Where to store logs | `%LOCALAPPDATA%\PoolIngest\logs` |
| `AzCopy.Enabled` | Enable Azure upload | `true` |
| `AzCopy.SasToken` | SAS token for blob access | Required for upload |

## Error Handling

The script handles:
- **Missing drives**: Validates drive exists before scanning
- **Access denied**: Skips folders that throw access errors (logged)
- **FFmpeg failures**: Captures exit codes and error output
- **FFprobe failures**: Requires valid metadata; fails fast if creation_time missing
- **Existing files**: Optionally skips re-processing (via `SkipIfExists`)
- **Zero-length files**: Automatically skipped (corrupted/incomplete recordings)
- **Upload retries**: Up to 3 attempts with progressive delay

## Examples

### Full workflow from USB drive
```powershell
# USB drive mounted as E: - convert, extract metadata, rename, and upload
.\video-processing.ps1 -DriveLetter E
```

### Process existing folder (recommended for already-converted files)
```powershell
# Files already in Videos\pool\2026-01-28 - just rename and upload
.\video-processing.ps1 -SourceDirectory "2026-01-28"
```

### See what would happen without processing
```powershell
.\video-processing.ps1 -SourceDirectory "2026-01-28" -WhatIf -SkipUpload
```

### Upload previously processed files
```powershell
.\video-processing.ps1 -UploadOnly
```

### Process with timestamp subfolder
Edit `config.json` to set `"IncludeTimestampInFolder": true`, then:
```powershell
.\video-processing.ps1 -DriveLetter D
# Creates: Videos\pool\2026-01-18\143052\
```

## Troubleshooting

### "FFmpeg not found"
- Verify ffmpeg is installed: `ffmpeg -version`
- If not in PATH, set full path in `config.json`: `"FfmpegPath": "C:\\ffmpeg\\bin\\ffmpeg.exe"`

### "FFprobe not found"
- Usually installed with ffmpeg
- Verify: `ffprobe -version`
- Set full path in `config.json`: `"FfprobePath": "C:\\ffmpeg\\bin\\ffprobe.exe"`

### "Metadata extraction failed"
- Video must have `creation_time` metadata embedded (most cameras do this)
- Check manually: `ffprobe -v error -show_entries format_tags=creation_time <file>`
- If missing, video was likely recorded with non-standard software

### "Drive does not exist"
- Ensure the drive letter is correct and the device is mounted
- Check in File Explorer that the drive is accessible

### "No video files found"
- Verify the drive contains `.mp4` files
- Check the log for excluded paths
- Ensure files aren't in excluded system folders
- Zero-length files are automatically skipped

### Files being skipped
- If `SkipIfExists` is `true`, existing output files won't be re-processed
- Delete existing files or set `"SkipIfExists": false` to force re-processing
- Already-renamed files (matching `YYYYMMDD_HHmmss_D*_` pattern) are skipped

### Azure upload fails
- Verify SAS token is valid and not expired
- Check `AzCopy.Enabled` is `true` in config
- Try running with `-SkipUpload` first to verify local processing works

## Version History

- **1.3.0-metadata** (2026-02-01): FFprobe metadata extraction
  - Extract creation_time UTC timestamp from video metadata
  - Extract video duration automatically
  - Smart file renaming with embedded metadata
  - New filename format: `YYYYMMDD_HHmmss_D{duration}_{seq}_{name}.mp4`
  - Same-second collision handling with sequence numbers
  - Skip zero-length corrupted files

- **1.2.0** (2026-01-28): Azure upload and SourceDirectory mode
  - AzCopy integration for Azure Blob Storage upload
  - `-SourceDirectory` parameter for existing folders
  - `-UploadOnly` mode for re-uploading
  - Retry logic with up to 3 attempts

- **1.0.0-phase1** (2026-01-18): Initial Phase 1 implementation
  - Core local conversion with ffmpeg faststart
  - Single drive scanning
  - Date-based output folders
  - Comprehensive logging

## Integration with Observations Feature

The renamed files integrate seamlessly with the SideSpins Observations feature:

1. **Run video-processing.ps1** to process and upload videos
2. **Open Observations page** in the SideSpins app
3. **Attach recordings** - the system parses filenames to extract:
   - `startTime` (DateTime) for exact video start
   - `durationSeconds` for video length
   - Automatic part ordering by recording time
4. **Add notes** with precise timestamps that correlate to real-world time
5. **Click notes** to seek directly to the correct position in the correct video part

See `_docs/OBSERVATIONS_SETUP.md` for the complete observations workflow.

## License

Proprietary - SideSpins Project

## Support

For issues or questions, check logs in `%LOCALAPPDATA%\PoolIngest\logs\` first.
