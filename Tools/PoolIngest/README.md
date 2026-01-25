# USB Video Ingest Utility

A PowerShell-based utility for ingesting raw video files from removable media, optimizing them for streaming with ffmpeg faststart, and organizing outputs into date-based folders.

## Current Status: Phase 1

**Phase 1** implements core local conversion functionality for a specified drive.

## Prerequisites

### Required Software

1. **FFmpeg** - Must be installed and accessible
   - Install via [ffmpeg.org](https://ffmpeg.org/download.html) or `winget install ffmpeg`
   - Ensure `ffmpeg` is in your PATH, or configure `FfmpegPath` in `config.json`
   - Verify: `ffmpeg -version`

### System Requirements

- Windows PowerShell 5.1+ or PowerShell 7+
- Write access to `%USERPROFILE%\Videos\pool`
- Write access to `%LOCALAPPDATA%\PoolIngest\logs`

## Quick Start

### Basic Usage

```powershell
# Process all MP4 files on drive D:
.\usb_ingest.ps1 -DriveLetter D

# Dry-run mode (shows what would be processed)
.\usb_ingest.ps1 -DriveLetter E -WhatIf
```

### What It Does

1. **Validates** the specified drive exists
2. **Scans** the entire drive recursively for `*.mp4` files
3. **Creates** output folder: `%USERPROFILE%\Videos\pool\YYYY-MM-DD\`
4. **Processes** each video with ffmpeg:
   - Copies streams (no re-encoding)
   - Relocates moov atom to front for streaming (`-movflags faststart`)
   - Saves with `_faststart` suffix
5. **Logs** summary of found/processed/skipped/failed files

### Output Structure

```
C:\Users\<YourName>\Videos\pool\
  └── 2026-01-18\
      ├── video1_faststart.mp4
      ├── video2_faststart.mp4
      └── ...
```

### Log Files

Logs are stored in: `%LOCALAPPDATA%\PoolIngest\logs\ingest_YYYY-MM-DD_HHmmss.log`

Each log includes:
- Timestamp for each operation
- Files found and processed
- Any errors or warnings
- Final summary statistics

## Configuration

Edit `config.json` to customize behavior:

```json
{
  "OutputRoot": "%USERPROFILE%\\Videos\\pool",
  "AppendSuffix": "_faststart",
  "FfmpegPath": "ffmpeg",
  "IncludeTimestampInFolder": false,
  "FileExtensions": [".mp4"],
  "SkipIfExists": true,
  "ExcludePaths": [
    "System Volume Information",
    "$RECYCLE.BIN",
    "RECYCLER"
  ],
  "LogDirectory": "%LOCALAPPDATA%\\PoolIngest\\logs"
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `OutputRoot` | Base directory for processed videos | `%USERPROFILE%\Videos\pool` |
| `AppendSuffix` | Suffix added to filenames | `_faststart` |
| `FfmpegPath` | Path to ffmpeg executable | `ffmpeg` (uses PATH) |
| `IncludeTimestampInFolder` | Add HHmmss to date folder | `false` |
| `FileExtensions` | File types to process | `[".mp4"]` |
| `SkipIfExists` | Skip if output file exists | `true` |
| `ExcludePaths` | Folder names to skip | System folders |
| `LogDirectory` | Where to store logs | `%LOCALAPPDATA%\PoolIngest\logs` |

## Error Handling

The script handles:
- **Missing drives**: Validates drive exists before scanning
- **Access denied**: Skips folders that throw access errors (logged)
- **FFmpeg failures**: Captures exit codes and error output
- **Existing files**: Optionally skips re-processing (via `SkipIfExists`)

## Examples

### Process USB drive
```powershell
# USB drive mounted as E:
.\usb_ingest.ps1 -DriveLetter E
```

### See what would happen without processing
```powershell
.\usb_ingest.ps1 -DriveLetter D -WhatIf
```

### Process with timestamp subfolder
Edit `config.json` to set `"IncludeTimestampInFolder": true`, then:
```powershell
.\usb_ingest.ps1 -DriveLetter D
# Creates: Videos\pool\2026-01-18\143052\
```

## Troubleshooting

### "FFmpeg not found"
- Verify ffmpeg is installed: `ffmpeg -version`
- If not in PATH, set full path in `config.json`: `"FfmpegPath": "C:\\ffmpeg\\bin\\ffmpeg.exe"`

### "Drive does not exist"
- Ensure the drive letter is correct and the device is mounted
- Check in File Explorer that the drive is accessible

### "No video files found"
- Verify the drive contains `.mp4` files
- Check the log for excluded paths
- Ensure files aren't in excluded system folders

### Files being skipped
- If `SkipIfExists` is `true`, existing output files won't be re-processed
- Delete existing files or set `"SkipIfExists": false` to force re-processing

## Roadmap

### Future Phases (Not Yet Implemented)

- **Phase 2**: Auto-discover removable drives (no `-DriveLetter` required)
- **Phase 3**: Upload to Azure Blob Storage via azcopy
- **Phase 4**: Idempotency checks, better exclusions, detailed reporting
- **Phase 5**: GUI wrapper, installer, documentation

## Version History

- **1.0.0-phase1** (2026-01-18): Initial Phase 1 implementation
  - Core local conversion with ffmpeg faststart
  - Single drive scanning
  - Date-based output folders
  - Comprehensive logging

## License

Proprietary - SideSpins Project

## Support

For issues or questions, check logs in `%LOCALAPPDATA%\PoolIngest\logs\` first.
