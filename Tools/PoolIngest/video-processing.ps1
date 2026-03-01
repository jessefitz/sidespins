<#
.SYNOPSIS
    USB Video Ingest - Local conversion, metadata extraction, and Azure upload

.DESCRIPTION
    Scans a specified drive for MP4 files, processes them with ffmpeg to relocate
    the moov atom for streaming optimization, extracts video metadata (creation time
    and duration) using ffprobe, renames files with timestamp prefixes for comment
    correlation, and uploads to Azure Blob Storage.

    Output filename format: YYYYMMDD_HHmmss_D{duration}_{seq}_{originalname}.mp4
    Example: 20260129_004011_D120_001_mvi_0066.mp4
    - 20260129_004011 = UTC creation time from video metadata
    - D120 = Duration in seconds (120s)
    - 001 = Sequence number (for same-second collisions)
    - mvi_0066 = Original filename

.PARAMETER Auto
    Automatically detect Canon video source. Checks for SD card reader first (fast),
    then falls back to MTP USB connection. Mutually exclusive with other source parameters.

.PARAMETER DriveLetter
    Drive letter to scan (e.g., 'D' or 'E'). Not required when using -UploadOnly, -SourceDirectory, or -Auto.

.PARAMETER SourceDirectory
    Name of a subdirectory under OutputRoot to use as the source. When specified, skips
    drive scanning and uses this directory directly. FFmpeg conversion is skipped unless
    -MovFlags is also specified.

.PARAMETER MovFlags
    When used with -SourceDirectory, enables ffmpeg conversion with movflags faststart.
    Without this flag, files from SourceDirectory are uploaded directly without conversion.

.PARAMETER UploadOnly
    Skip drive scanning and only upload existing files from the output directory.

.PARAMETER SkipUpload
    Skip Azure upload (process files only).

.PARAMETER WhatIf
    Dry-run mode. Shows what would be processed without making changes.

.EXAMPLE
    .\video-processing.ps1 -DriveLetter D
    Scans D:\ for MP4 files, converts with faststart, extracts metadata, renames, and uploads.

.EXAMPLE
    .\video-processing.ps1 -DriveLetter E -WhatIf
    Shows what would be processed on E:\ without making changes.

.EXAMPLE
    .\video-processing.ps1 -UploadOnly
    Extract metadata, rename, and upload today's processed videos to Azure.

.EXAMPLE
    .\video-processing.ps1 -DriveLetter D -SkipUpload
    Process files from D:\, extract metadata, and rename (no upload).

.EXAMPLE
    .\video-processing.ps1 -SourceDirectory "2026-01-28"
    Extract metadata, rename, and upload files from the 2026-01-28 folder.

.EXAMPLE
    .\video-processing.ps1 -SourceDirectory "2026-01-28" -MovFlags
    Process with ffmpeg faststart, extract metadata, rename, then upload.

.EXAMPLE
    .\video-processing.ps1 -Auto
    Auto-detect Canon camera (SD card or USB), process, rename, and upload.

.EXAMPLE
    .\video-processing.ps1 -Auto -SkipUpload
    Auto-detect Canon camera, process and rename locally (no upload).

.NOTES
    Requires: ffmpeg, ffprobe, azcopy (all must be in PATH or configured in config.json)
    Videos must have creation_time metadata embedded (most cameras do this automatically).
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory=$false)]
    [ValidatePattern('^[A-Za-z]$')]
    [string]$DriveLetter,

    [Parameter(Mandatory=$false)]
    [string]$SourceDirectory,

    [Parameter(Mandatory=$false)]
    [switch]$MovFlags,

    [Parameter(Mandatory=$false)]
    [switch]$UploadOnly,

    [Parameter(Mandatory=$false)]
    [switch]$SkipUpload,

    [Parameter(Mandatory=$false)]
    [switch]$Auto
)

# Script version
$ScriptVersion = "1.4.0"

# Validate parameter combinations
$paramCount = 0
if ($DriveLetter) { $paramCount++ }
if ($SourceDirectory) { $paramCount++ }
if ($UploadOnly) { $paramCount++ }
if ($Auto) { $paramCount++ }

if ($paramCount -gt 1) {
    Write-Error "Cannot specify more than one of: -DriveLetter, -SourceDirectory, -UploadOnly, or -Auto"
    exit 1
}

if ($paramCount -eq 0) {
    Write-Error "Must specify one of: -DriveLetter, -SourceDirectory, -UploadOnly, or -Auto"
    exit 1
}

if ($MovFlags -and -not $SourceDirectory) {
    Write-Error "-MovFlags can only be used with -SourceDirectory"
    exit 1
}

#region Configuration Loading
function Get-Config {
    $configPath = Join-Path $PSScriptRoot "config.json"
    
    if (-not (Test-Path $configPath)) {
        throw "Configuration file not found: $configPath"
    }
    
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Expand environment variables
        $config.OutputRoot = [Environment]::ExpandEnvironmentVariables($config.OutputRoot)
        $config.LogDirectory = [Environment]::ExpandEnvironmentVariables($config.LogDirectory)
        
        return $config
    }
    catch {
        throw "Failed to load configuration: $_"
    }
}
#endregion

#region Logging
$Script:LogFilePath = $null
$Script:Stats = @{
    Found = 0
    Processed = 0
    Skipped = 0
    Failed = 0
    Errors = @()
}

function Initialize-Logging {
    param([string]$LogDirectory)
    
    # Create log directory if it doesn't exist
    if (-not (Test-Path $LogDirectory)) {
        New-Item -Path $LogDirectory -ItemType Directory -Force | Out-Null
    }
    
    # Create log file with timestamp
    $timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
    $Script:LogFilePath = Join-Path $LogDirectory "ingest_$timestamp.log"
    
    Write-Log "=== USB Video Ingest Started ===" -Level INFO
    Write-Log "Version: $ScriptVersion" -Level INFO
    Write-Log "Log file: $Script:LogFilePath" -Level INFO
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR', 'SUCCESS')]
        [string]$Level = 'INFO'
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    # Write to console with color
    $color = switch ($Level) {
        'INFO'    { 'White' }
        'WARN'    { 'Yellow' }
        'ERROR'   { 'Red' }
        'SUCCESS' { 'Green' }
    }
    Write-Host $logEntry -ForegroundColor $color
    
    # Write to log file
    if ($Script:LogFilePath) {
        Add-Content -Path $Script:LogFilePath -Value $logEntry
    }
}
#endregion

#region Camera Auto-Detection
function Find-CanonSDCard {
    param(
        [string]$FolderPattern = '^\d{3}CANON$'
    )

    Write-Log "Scanning for Canon SD card on removable drives..." -Level INFO

    try {
        $removableDrives = Get-WmiObject -Class Win32_LogicalDisk -Filter "DriveType=2" -ErrorAction SilentlyContinue

        if (-not $removableDrives) {
            Write-Log "No removable drives found" -Level INFO
            return $null
        }

        foreach ($drive in $removableDrives) {
            $driveLetter = $drive.DeviceID.TrimEnd(':')
            $dcimPath = Join-Path "$($drive.DeviceID)\" "DCIM"

            if (Test-Path $dcimPath) {
                $canonFolders = Get-ChildItem -Path $dcimPath -Directory -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -match $FolderPattern }

                if ($canonFolders -and $canonFolders.Count -gt 0) {
                    Write-Log "Found Canon SD card on drive $($drive.DeviceID) (Label: $($drive.VolumeName))" -Level SUCCESS
                    Write-Log "  DCIM folders: $($canonFolders.Name -join ', ')" -Level INFO
                    return $driveLetter
                }
            }
        }

        Write-Log "No Canon SD card found on removable drives" -Level INFO
        return $null
    }
    catch {
        Write-Log "Error scanning for SD card: $_" -Level ERROR
        return $null
    }
}

function Copy-FromMTPDevice {
    param(
        [string]$DeviceNamePattern = 'Canon',
        [string]$DestinationPath,
        [string[]]$FileExtensions = @('.mp4')
    )

    Write-Log "Scanning for MTP device matching '$DeviceNamePattern'..." -Level INFO

    try {
        $shell = New-Object -ComObject Shell.Application
        $thisPC = $shell.Namespace(0x11)  # "This PC"

        if (-not $thisPC) {
            Write-Log "Could not access 'This PC' namespace" -Level ERROR
            return $null
        }

        # Find the device matching the pattern
        $device = $null
        foreach ($item in $thisPC.Items()) {
            if ($item.Name -match $DeviceNamePattern) {
                $device = $item
                Write-Log "Found MTP device: $($item.Name)" -Level SUCCESS
                break
            }
        }

        if (-not $device) {
            Write-Log "No MTP device found matching '$DeviceNamePattern'" -Level INFO
            return $null
        }

        # Recursively find video files on the MTP device
        $mtpFiles = @()
        Find-MTPFiles -Folder $device.GetFolder -Files ([ref]$mtpFiles) -Extensions $FileExtensions

        if ($mtpFiles.Count -eq 0) {
            Write-Log "No video files found on MTP device" -Level WARN
            return $null
        }

        Write-Log "Found $($mtpFiles.Count) video file(s) on MTP device" -Level SUCCESS

        # Ensure destination directory exists
        if (-not (Test-Path $DestinationPath)) {
            New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
            Write-Log "Created output directory: $DestinationPath" -Level INFO
        }

        $destShell = $shell.Namespace($DestinationPath)
        if (-not $destShell) {
            Write-Log "Could not access destination folder: $DestinationPath" -Level ERROR
            return $null
        }

        $copiedFiles = @()
        $fileIndex = 0
        foreach ($mtpFile in $mtpFiles) {
            $fileIndex++
            $fileName = $mtpFile.Name
            $destFilePath = Join-Path $DestinationPath $fileName
            $progress = "[$fileIndex/$($mtpFiles.Count)]"

            # Skip if already exists and non-empty
            if (Test-Path $destFilePath) {
                $existingFile = Get-Item $destFilePath
                if ($existingFile.Length -gt 0) {
                    Write-Log "  $progress Skipped (already exists): $fileName ($([Math]::Round($existingFile.Length / 1MB, 1)) MB)" -Level WARN
                    $copiedFiles += $existingFile
                    continue
                }
                else {
                    Write-Log "  $progress Removing empty file, will re-copy: $fileName" -Level WARN
                    Remove-Item $destFilePath -Force
                }
            }

            Write-Log "  $progress Copying: $fileName ..." -Level INFO

            if ($WhatIfPreference) {
                Write-Log "  $progress [WhatIf] Would copy: $fileName" -Level INFO
                continue
            }

            # CopyHere: 0x10 = overwrite, 0x4 = no progress dialog
            $destShell.CopyHere($mtpFile, 0x14)

            # Poll for completion (MTP copies are async)
            $timeoutSeconds = 600  # 10 minutes per file
            $zeroLengthTimeout = 30  # bail out after 30s if file stays at 0 bytes
            $elapsed = 0
            $pollInterval = 2

            # Wait for file to appear
            while (-not (Test-Path $destFilePath) -and $elapsed -lt $timeoutSeconds) {
                Start-Sleep -Seconds $pollInterval
                $elapsed += $pollInterval
                if ($elapsed % 10 -eq 0) {
                    Write-Log "  $progress Waiting for file to appear... (${elapsed}s)" -Level INFO
                }
            }

            if (Test-Path $destFilePath) {
                # Wait for file to finish writing (size stabilizes)
                $lastSize = 0
                $stableCount = 0
                $zeroElapsed = 0
                while ($stableCount -lt 3 -and $elapsed -lt $timeoutSeconds) {
                    Start-Sleep -Seconds $pollInterval
                    $elapsed += $pollInterval
                    $currentSize = (Get-Item $destFilePath).Length
                    if ($currentSize -eq 0) {
                        $zeroElapsed += $pollInterval
                        if ($zeroElapsed -ge $zeroLengthTimeout) {
                            Write-Log "  $progress File stuck at 0 bytes after ${zeroElapsed}s, skipping: $fileName" -Level WARN
                            break
                        }
                    }
                    elseif ($currentSize -eq $lastSize) {
                        $stableCount++
                    }
                    else {
                        $stableCount = 0
                        if ($elapsed % 10 -eq 0) {
                            Write-Log "  $progress Copying... $([Math]::Round($currentSize / 1MB, 1)) MB (${elapsed}s)" -Level INFO
                        }
                    }
                    $lastSize = $currentSize
                }

                if ($stableCount -ge 3 -and $lastSize -gt 0) {
                    Write-Log "  $progress Copied: $fileName ($([Math]::Round($lastSize / 1MB, 1)) MB, ${elapsed}s)" -Level SUCCESS
                    $copiedFiles += Get-Item $destFilePath
                }
                elseif ($lastSize -eq 0) {
                    Write-Log "  $progress Skipped (copy produced empty file): $fileName" -Level WARN
                    Remove-Item $destFilePath -Force -ErrorAction SilentlyContinue
                    $Script:Stats.Skipped++
                }
                else {
                    Write-Log "  $progress Copy timed out after ${elapsed}s: $fileName ($([Math]::Round($lastSize / 1MB, 1)) MB)" -Level ERROR
                    $Script:Stats.Failed++
                    $Script:Stats.Errors += "MTP copy timed out for $fileName after ${elapsed}s"
                }
            }
            else {
                Write-Log "  $progress Copy failed - file not created after ${elapsed}s: $fileName" -Level ERROR
                $Script:Stats.Failed++
                $Script:Stats.Errors += "MTP copy failed for $fileName"
            }
        }

        Write-Log "MTP copy complete: $($copiedFiles.Count) of $($mtpFiles.Count) file(s) copied" -Level SUCCESS
        return $copiedFiles
    }
    catch {
        Write-Log "Error during MTP copy: $_" -Level ERROR
        return $null
    }
}

function Find-MTPFiles {
    param(
        [object]$Folder,
        [ref]$Files,
        [string[]]$Extensions
    )

    if (-not $Folder) { return }

    try {
        foreach ($item in $Folder.Items()) {
            if ($item.IsFolder) {
                Find-MTPFiles -Folder $item.GetFolder -Files $Files -Extensions $Extensions
            }
            else {
                foreach ($ext in $Extensions) {
                    if ($item.Name -like "*$ext") {
                        $Files.Value += $item
                        break
                    }
                }
            }
        }
    }
    catch {
        Write-Log "  Warning: Could not enumerate MTP folder: $_" -Level WARN
    }
}
#endregion

#region Drive Validation
function Test-DriveExists {
    param([string]$DriveLetter)
    
    $drivePath = "${DriveLetter}:\"
    
    if (-not (Test-Path $drivePath)) {
        Write-Log "Drive $drivePath does not exist or is not accessible" -Level ERROR
        return $false
    }
    
    try {
        $drive = Get-PSDrive -Name $DriveLetter -ErrorAction Stop
        Write-Log "Drive validated: $drivePath (Label: $($drive.Description))" -Level INFO
        return $true
    }
    catch {
        Write-Log "Failed to access drive ${DriveLetter}: $_" -Level ERROR
        return $false
    }
}
#endregion

#region File Discovery
function Find-VideoFiles {
    param(
        [string]$RootPath,
        [string[]]$Extensions,
        [string[]]$ExcludePaths
    )
    
    Write-Log "Scanning $RootPath for video files..." -Level INFO
    $foundFiles = @()
    
    try {
        # Build exclusion pattern
        $excludePattern = if ($ExcludePaths -and $ExcludePaths.Count -gt 0) {
            "($($ExcludePaths -join '|'))"
        } else {
            $null
        }
        
        # Recursively find files
        Get-ChildItem -Path $RootPath -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            # Check if file matches extensions
            $matchesExtension = $false
            foreach ($ext in $Extensions) {
                if ($_.Extension -eq $ext) {
                    $matchesExtension = $true
                    break
                }
            }
            
            if (-not $matchesExtension) {
                return
            }
            
            # Check if path should be excluded
            if ($excludePattern -and $_.FullName -match $excludePattern) {
                Write-Log "Excluded: $($_.FullName)" -Level WARN
                return
            }
            
            # Skip zero-length files
            if ($_.Length -eq 0) {
                Write-Log "Skipped (zero length): $($_.FullName)" -Level WARN
                return
            }
            
            $foundFiles += $_
        }
        
        $Script:Stats.Found = $foundFiles.Count
        Write-Log "Found $($foundFiles.Count) video file(s)" -Level SUCCESS
        
        return $foundFiles
    }
    catch {
        Write-Log "Error during file discovery: $_" -Level ERROR
        $Script:Stats.Errors += "File discovery error: $_"
        return @()
    }
}
#endregion

#region Output Directory
function New-OutputDirectory {
    param(
        [string]$OutputRoot,
        [bool]$IncludeTimestamp
    )
    
    $dateFolder = Get-Date -Format "yyyy-MM-dd"
    
    if ($IncludeTimestamp) {
        $timeFolder = Get-Date -Format "HHmmss"
        $outputPath = Join-Path $OutputRoot "$dateFolder\$timeFolder"
    }
    else {
        $outputPath = Join-Path $OutputRoot $dateFolder
    }
    
    if (-not (Test-Path $outputPath)) {
        New-Item -Path $outputPath -ItemType Directory -Force | Out-Null
        Write-Log "Created output directory: $outputPath" -Level INFO
    }
    else {
        Write-Log "Using existing output directory: $outputPath" -Level INFO
    }
    
    return $outputPath
}
#endregion

#region FFmpeg Processing
function Test-NeedsFaststart {
    param(
        [string]$FilePath,
        [string]$FfprobePath
    )

    # Use ffprobe trace output to check if moov atom appears before mdat.
    # If moov is already first, no ffmpeg processing needed.
    # Conservative: if we can't determine atom order, return $true (needs processing).
    try {
        $traceOutput = & $FfprobePath -v trace -i $FilePath 2>&1 | Out-String

        $moovPos = $traceOutput.IndexOf("type:'moov'")
        $mdatPos = $traceOutput.IndexOf("type:'mdat'")

        if ($moovPos -lt 0 -or $mdatPos -lt 0) {
            Write-Log "  Could not determine atom order for $(Split-Path $FilePath -Leaf) - will process with ffmpeg" -Level WARN
            return $true
        }

        if ($moovPos -lt $mdatPos) {
            Write-Log "  moov atom already before mdat - faststart not needed" -Level INFO
            return $false
        }
        else {
            Write-Log "  moov atom after mdat - faststart processing needed" -Level INFO
            return $true
        }
    }
    catch {
        Write-Log "  Error checking atom order: $_ - will process with ffmpeg" -Level WARN
        return $true
    }
}

function Test-FfmpegAvailable {
    param([string]$FfmpegPath)
    
    try {
        $result = & $FfmpegPath -version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $versionLine = ($result | Select-Object -First 1) -replace 'ffmpeg version ', ''
            Write-Log "FFmpeg found: $versionLine" -Level SUCCESS
            return $true
        }
    }
    catch {
        Write-Log "FFmpeg not found at: $FfmpegPath" -Level ERROR
        Write-Log "Error: $_" -Level ERROR
        return $false
    }
    
    return $false
}

function Convert-VideoFile {
    param(
        [System.IO.FileInfo]$SourceFile,
        [string]$DestinationPath,
        [string]$Suffix,
        [string]$FfmpegPath,
        [string]$FfprobePath,
        [bool]$SkipIfExists
    )

    # Build destination filename
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($SourceFile.Name)
    $extension = $SourceFile.Extension
    $destFileName = "${baseName}${Suffix}${extension}"
    $destFilePath = Join-Path $DestinationPath $destFileName

    # Check if already exists
    if ((Test-Path $destFilePath) -and $SkipIfExists) {
        Write-Log "Skipped (already exists): $destFileName" -Level WARN
        $Script:Stats.Skipped++
        return $true
    }

    # Check if faststart processing is actually needed
    $needsFaststart = $true
    if ($FfprobePath) {
        $needsFaststart = Test-NeedsFaststart -FilePath $SourceFile.FullName -FfprobePath $FfprobePath
    }

    if (-not $needsFaststart) {
        # moov atom already before mdat - just copy the file
        Write-Log "Copying (faststart not needed): $($SourceFile.Name) -> $destFileName" -Level INFO

        if ($WhatIfPreference) {
            Write-Log "[WhatIf] Would copy: $($SourceFile.FullName) -> $destFilePath" -Level INFO
            return $true
        }

        try {
            Copy-Item -Path $SourceFile.FullName -Destination $destFilePath -Force
            Write-Log "Successfully copied: $destFileName" -Level SUCCESS
            $Script:Stats.Processed++
            return $true
        }
        catch {
            Write-Log "Copy failed: $_" -Level ERROR
            $Script:Stats.Failed++
            $Script:Stats.Errors += "Failed to copy $($SourceFile.Name): $_"
            return $false
        }
    }

    Write-Log "Processing: $($SourceFile.Name) -> $destFileName" -Level INFO

    # Run ffmpeg
    try {
        $ffmpegArgs = @(
            '-i', "`"$($SourceFile.FullName)`""
            '-c', 'copy'
            '-movflags', 'faststart'
            '-y'  # Overwrite if exists
            "`"$destFilePath`""
        )

        if ($WhatIfPreference) {
            Write-Log "[WhatIf] Would run: $FfmpegPath $($ffmpegArgs -join ' ')" -Level INFO
            return $true
        }

        $process = Start-Process -FilePath $FfmpegPath `
                                  -ArgumentList $ffmpegArgs `
                                  -NoNewWindow `
                                  -Wait `
                                  -PassThru `
                                  -RedirectStandardError (Join-Path $env:TEMP "ffmpeg_error.txt")

        if ($process.ExitCode -eq 0) {
            Write-Log "Successfully processed: $destFileName" -Level SUCCESS
            $Script:Stats.Processed++
            return $true
        }
        else {
            $errorContent = Get-Content (Join-Path $env:TEMP "ffmpeg_error.txt") -Raw
            Write-Log "FFmpeg failed with exit code $($process.ExitCode)" -Level ERROR
            Write-Log "Error output: $errorContent" -Level ERROR
            $Script:Stats.Failed++
            $Script:Stats.Errors += "Failed to process $($SourceFile.Name): Exit code $($process.ExitCode)"
            return $false
        }
    }
    catch {
        Write-Log "Exception during conversion: $_" -Level ERROR
        $Script:Stats.Failed++
        $Script:Stats.Errors += "Exception processing $($SourceFile.Name): $_"
        return $false
    }
}
#endregion

#region FFprobe Metadata Extraction
function Test-FfprobeAvailable {
    param([string]$FfprobePath)
    
    try {
        $result = & $FfprobePath -version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $versionLine = ($result | Select-Object -First 1) -replace 'ffprobe version ', ''
            Write-Log "FFprobe found: $versionLine" -Level SUCCESS
            return $true
        }
    }
    catch {
        Write-Log "FFprobe not found at: $FfprobePath" -Level ERROR
        Write-Log "Error: $_" -Level ERROR
        return $false
    }
    
    return $false
}

function Get-VideoMetadata {
    param(
        [System.IO.FileInfo]$VideoFile,
        [string]$FfprobePath
    )
    
    $metadata = @{
        CreationTime = $null
        DurationSeconds = $null
        Success = $false
    }
    
    try {
        # Get creation_time from format tags
        $creationOutput = & $FfprobePath -v error -show_entries format_tags=creation_time -of default=nw=1:nk=1 $VideoFile.FullName 2>&1
        
        if ($LASTEXITCODE -eq 0 -and $creationOutput) {
            $creationTimeStr = ($creationOutput | Out-String).Trim()
            if ($creationTimeStr -match '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}') {
                # Parse ISO 8601 UTC timestamp (e.g., 2026-01-29T00:40:11.000000Z)
                $metadata.CreationTime = [DateTime]::Parse($creationTimeStr).ToUniversalTime()
                Write-Log "  Creation time: $($metadata.CreationTime.ToString('yyyy-MM-dd HH:mm:ss')) UTC" -Level INFO
            }
        }
        
        # Get duration from format
        $durationOutput = & $FfprobePath -v error -show_entries format=duration -of default=nw=1:nk=1 $VideoFile.FullName 2>&1
        
        if ($LASTEXITCODE -eq 0 -and $durationOutput) {
            $durationStr = ($durationOutput | Out-String).Trim()
            if ($durationStr -match '^[\d.]+$') {
                $metadata.DurationSeconds = [Math]::Round([double]$durationStr)
                Write-Log "  Duration: $($metadata.DurationSeconds) seconds" -Level INFO
            }
        }
        
        if ($metadata.CreationTime -and $metadata.DurationSeconds) {
            $metadata.Success = $true
        }
        else {
            Write-Log "  Warning: Could not extract complete metadata" -Level WARN
            if (-not $metadata.CreationTime) {
                Write-Log "    - Missing creation_time" -Level WARN
            }
            if (-not $metadata.DurationSeconds) {
                Write-Log "    - Missing duration" -Level WARN
            }
        }
    }
    catch {
        Write-Log "  Error extracting metadata: $_" -Level ERROR
    }
    
    return $metadata
}

function Rename-VideosWithMetadata {
    param(
        [string]$DirectoryPath,
        [string]$FfprobePath
    )
    
    Write-Log "=== Phase 2: Metadata Extraction & Rename ===" -Level INFO
    
    # Check ffprobe availability (fail if not found)
    if (-not (Test-FfprobeAvailable -FfprobePath $FfprobePath)) {
        throw "FFprobe is not available. Please install ffprobe and ensure it's in PATH or configure FfprobePath in config.json"
    }
    
    # Find video files that haven't been renamed yet (don't have timestamp prefix)
    # Also skip zero-length files (corrupted/incomplete recordings)
    $videoFiles = Get-ChildItem -Path $DirectoryPath -File | 
        Where-Object { $_.Extension -match '\.mp4$' } |
        Where-Object { $_.Length -gt 0 } |
        Where-Object { $_.Name -notmatch '^\d{8}_\d{6}_D\d+_' }  # Skip already renamed files
    
    if ($videoFiles.Count -eq 0) {
        Write-Log "No files need metadata extraction (all files already renamed or no MP4 files found)" -Level INFO
        return $true
    }
    
    Write-Log "Extracting metadata from $($videoFiles.Count) file(s)..." -Level INFO
    
    # Collect metadata for all files first (for collision detection)
    $fileMetadata = @()
    foreach ($file in $videoFiles) {
        Write-Log "Processing: $($file.Name)" -Level INFO
        $metadata = Get-VideoMetadata -VideoFile $file -FfprobePath $FfprobePath
        
        if (-not $metadata.Success) {
            Write-Log "Failed to extract metadata from $($file.Name) - cannot proceed" -Level ERROR
            $Script:Stats.Failed++
            $Script:Stats.Errors += "Metadata extraction failed for $($file.Name)"
            throw "Metadata extraction failed for $($file.Name). All videos must have valid creation_time and duration."
        }
        
        $fileMetadata += @{
            File = $file
            CreationTime = $metadata.CreationTime
            DurationSeconds = $metadata.DurationSeconds
        }
    }
    
    # Sort by creation time for sequence numbering
    $fileMetadata = $fileMetadata | Sort-Object { $_.CreationTime }
    
    # Track timestamps for collision detection (same second)
    $timestampCounts = @{}
    
    Write-Log "" -Level INFO
    Write-Log "Renaming files with metadata..." -Level INFO
    
    foreach ($item in $fileMetadata) {
        $file = $item.File
        $creationTime = $item.CreationTime
        $duration = $item.DurationSeconds
        
        # Format: YYYYMMDD_HHmmss
        $timestampKey = $creationTime.ToString('yyyyMMdd_HHmmss')
        
        # Handle collisions (multiple videos in same second)
        if ($timestampCounts.ContainsKey($timestampKey)) {
            $timestampCounts[$timestampKey]++
        }
        else {
            $timestampCounts[$timestampKey] = 1
        }
        $sequence = $timestampCounts[$timestampKey]
        
        # Get original filename without extension
        $originalBaseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $extension = $file.Extension
        
        # Build new filename: YYYYMMDD_HHmmss_D{duration}_{sequence}_{originalName}.mp4
        $newFileName = "{0}_D{1}_{2:D3}_{3}{4}" -f $timestampKey, $duration, $sequence, $originalBaseName, $extension
        $newFilePath = Join-Path $DirectoryPath $newFileName
        
        # Check if target already exists
        if (Test-Path $newFilePath) {
            Write-Log "  Skipped (target exists): $newFileName" -Level WARN
            $Script:Stats.Skipped++
            continue
        }
        
        if ($WhatIfPreference) {
            Write-Log "  [WhatIf] Would rename: $($file.Name) -> $newFileName" -Level INFO
        }
        else {
            try {
                Rename-Item -Path $file.FullName -NewName $newFileName -ErrorAction Stop
                Write-Log "  Renamed: $($file.Name) -> $newFileName" -Level SUCCESS
            }
            catch {
                Write-Log "  Failed to rename $($file.Name): $_" -Level ERROR
                $Script:Stats.Failed++
                $Script:Stats.Errors += "Failed to rename $($file.Name): $_"
                throw "Failed to rename $($file.Name): $_"
            }
        }
    }
    
    Write-Log "" -Level INFO
    Write-Log "=== Metadata Extraction Complete ===" -Level SUCCESS
    return $true
}
#endregion

#region AzCopy Upload
function Test-AzCopyAvailable {
    param([string]$AzCopyPath)
    
    try {
        $result = & $AzCopyPath --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $versionLine = ($result | Select-Object -First 1)
            Write-Log "AzCopy found: $versionLine" -Level SUCCESS
            return $true
        }
    }
    catch {
        Write-Log "AzCopy not found at: $AzCopyPath" -Level ERROR
        Write-Log "Error: $_" -Level ERROR
        return $false
    }
    
    return $false
}

function Start-AzureUpload {
    param(
        [string]$LocalPath,
        [PSCustomObject]$AzCopyConfig
    )

    Write-Log "=== Starting Azure Upload ===" -Level INFO

    # Create marker file
    $markerInProgress = Join-Path $LocalPath "_upload_in_progress.txt"
    $markerComplete = Join-Path $LocalPath "_upload_complete.txt"
    $markerFailed = Join-Path $LocalPath "_upload_failed.txt"

    # Clean up old markers
    Remove-Item $markerComplete -ErrorAction SilentlyContinue
    Remove-Item $markerFailed -ErrorAction SilentlyContinue

    if (-not $WhatIfPreference) {
        Set-Content -Path $markerInProgress -Value "Upload started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        Write-Log "Created upload marker: $markerInProgress" -Level INFO
    }

    # Build destination URL from config parts
    $folderName = Split-Path $LocalPath -Leaf

    $remotePathParts = @()
    if ($AzCopyConfig.RemotePrefix) {
        $remotePathParts += $AzCopyConfig.RemotePrefix.Trim('/')
    }
    $remotePath = ($remotePathParts -join '/')

    $baseUrl = $AzCopyConfig.BaseUrl.TrimEnd('/')
    $containerName = $AzCopyConfig.ContainerName.Trim('/')

    # Normalize SAS token
    $sasToken = $AzCopyConfig.SasToken.Trim()
    $sasToken = $sasToken.TrimStart('?')

    # Build the destination URL for recursive copy (azcopy will create the folder structure)
    if ($remotePath) {
        $destinationUrl = "$baseUrl/$containerName/$remotePath/$folderName`?$sasToken"
        $remoteFolder = "$baseUrl/$containerName/$remotePath/$folderName"
        $remoteFolderForList = "$remotePath/$folderName"
    }
    else {
        $destinationUrl = "$baseUrl/$containerName/$folderName`?$sasToken"
        $remoteFolder = "$baseUrl/$containerName/$folderName"
        $remoteFolderForList = $folderName
    }

    Write-Log "Uploading from: $LocalPath" -Level INFO
    Write-Log "Destination: $remoteFolder" -Level INFO

    # Count local files to upload
    $includePatterns = if ($AzCopyConfig.IncludePattern) {
        $AzCopyConfig.IncludePattern -split ';'
    } else {
        @('*')
    }

    $filesToUpload = @()
    foreach ($pattern in $includePatterns) {
        $filesToUpload += Get-ChildItem -Path $LocalPath -Filter $pattern -File
    }
    $filesToUpload = $filesToUpload | Sort-Object -Property FullName -Unique

    if ($filesToUpload.Count -eq 0) {
        Write-Log "No files found to upload in $LocalPath" -Level WARN
        return $false
    }

    Write-Log "Found $($filesToUpload.Count) file(s) to upload" -Level INFO

    # Check which files already exist remotely (informational)
    try {
        $listUrl = "$baseUrl/$containerName/$remoteFolderForList`?$sasToken"
        $listOutput = & $AzCopyConfig.AzCopyPath list $listUrl 2>&1
        $remoteCount = 0

        if ($LASTEXITCODE -eq 0) {
            $listOutput | ForEach-Object {
                if ($_ -match '; Content Length: ') {
                    $remoteCount++
                }
            }
            Write-Log "Found $remoteCount file(s) already in remote location" -Level INFO
        }
    }
    catch {
        Write-Log "Could not list remote files: $_" -Level WARN
    }

    # Build azcopy args for recursive upload
    $includePatternArg = $AzCopyConfig.IncludePattern
    $sourcePath = "$LocalPath/*"

    $azcopyArgs = @(
        'copy',
        $sourcePath,
        $destinationUrl,
        '--recursive',
        '--log-level=WARNING'
    )

    if ($includePatternArg) {
        $azcopyArgs += "--include-pattern=$includePatternArg"
    }

    if ($AzCopyConfig.OverwritePolicy) {
        $azcopyArgs += "--overwrite=$($AzCopyConfig.OverwritePolicy)"
    }

    if ($WhatIfPreference) {
        Write-Log "[WhatIf] Would run: $($AzCopyConfig.AzCopyPath) $($azcopyArgs -join ' ')" -Level INFO
        Remove-Item $markerInProgress -ErrorAction SilentlyContinue
        Set-Content -Path $markerComplete -Value "Upload completed (dry-run): $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        return $true
    }

    # Calculate total size for progress reporting
    $totalSizeBytes = ($filesToUpload | Measure-Object -Property Length -Sum).Sum
    $totalSizeGB = [Math]::Round($totalSizeBytes / 1GB, 1)
    $avgSizeMB = [Math]::Round($totalSizeBytes / $filesToUpload.Count / 1MB, 0)
    Write-Log "Starting recursive azcopy upload ($($filesToUpload.Count) files, $totalSizeGB GB, avg $avgSizeMB MB/file)..." -Level INFO
    Write-Log "Note: Progress updates every 30s. Each file must fully upload before showing as 'Done'." -Level INFO

    try {
        # Stream azcopy output line by line for real-time status
        $jobId = $null
        $uploadedCount = 0
        $failedCount = 0
        $startTime = Get-Date
        $Script:LastProgressTime = $startTime

        & $AzCopyConfig.AzCopyPath @azcopyArgs 2>&1 | ForEach-Object {
            $line = $_.ToString()

            # Extract job ID
            if ($line -match 'Job ([0-9a-f-]+) has started') {
                $jobId = $Matches[1]
                Write-Log "  azcopy job: $($Matches[1])" -Level INFO
            }
            # Track individual file completions
            elseif ($line -match 'UPLOADSUCCESSFUL') {
                $uploadedCount++
                if ($line -match '/([^/;]+\.MP4)') {
                    Write-Log "  [$uploadedCount/$($filesToUpload.Count)] Uploaded: $($Matches[1])" -Level SUCCESS
                }
                else {
                    Write-Log "  [$uploadedCount/$($filesToUpload.Count)] Uploaded" -Level SUCCESS
                }
            }
            elseif ($line -match 'UPLOADFAILED') {
                $failedCount++
                Write-Log "  azcopy FAILED: $line" -Level ERROR
            }
            # Progress lines (e.g., "0.0 %, 0 Done, 0 Failed, 135 Pending, ...")
            # azcopy uses \r to overwrite lines — split on \r and take the last non-empty segment
            elseif ($line -match '%.*Done.*Pending') {
                $now = Get-Date
                $elapsed = [Math]::Round(($now - $startTime).TotalSeconds)
                $sinceLast = ($now - $Script:LastProgressTime).TotalSeconds
                # Log every 30 seconds
                if ($sinceLast -ge 30) {
                    $Script:LastProgressTime = $now
                    # Extract the latest progress update (azcopy uses \r to overwrite)
                    $segments = $line -split "`r"
                    $latest = ($segments | Where-Object { $_.Trim() -ne '' } | Select-Object -Last 1).Trim()
                    # Extract done count for file-level progress
                    $doneMatch = [regex]::Match($latest, '(\d+)\s+Done')
                    if ($doneMatch.Success) { $uploadedCount = [int]$doneMatch.Groups[1].Value }
                    Write-Log "  azcopy: $latest (${elapsed}s elapsed)" -Level INFO
                }
            }
            # Show final job summary
            elseif ($line -match 'Final Job Status:|Elapsed Time|Total Number Of Transfers|Number of File Transfers|TotalBytesTransferred') {
                Write-Log "  azcopy: $line" -Level INFO
            }
            # Show real errors (not progress lines that happen to contain "Failed")
            elseif ($line -match 'AuthenticationFailed|AuthorizationFailure|ServerBusy|FATAL|cannot|unable' -and $line -notmatch 'log-level|AZCOPY_LOG_LOCATION') {
                Write-Log "  azcopy: $line" -Level ERROR
            }
        }

        $exitCode = $LASTEXITCODE
        $elapsed = [Math]::Round(((Get-Date) - $startTime).TotalSeconds)

        if ($exitCode -eq 0) {
            Write-Log "" -Level SUCCESS
            Write-Log "All files uploaded successfully ($uploadedCount files, ${elapsed}s)" -Level SUCCESS
            Remove-Item $markerInProgress -ErrorAction SilentlyContinue
            Set-Content -Path $markerComplete -Value "Upload completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n$uploadedCount files uploaded in ${elapsed}s"
            return $true
        }
        else {
            Write-Log "" -Level ERROR
            Write-Log "AzCopy upload failed with exit code $exitCode ($uploadedCount uploaded, $failedCount failed, ${elapsed}s)" -Level ERROR
            if ($jobId) {
                Write-Log "To resume this upload, run:" -Level WARN
                Write-Log "  azcopy jobs resume $jobId" -Level WARN
            }
            $Script:Stats.Errors += "AzCopy upload failed with exit code $exitCode"
            Remove-Item $markerInProgress -ErrorAction SilentlyContinue
            Set-Content -Path $markerFailed -Value "Upload failed (exit code $exitCode): $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            return $false
        }
    }
    catch {
        Write-Log "Exception during azcopy upload: $_" -Level ERROR
        $Script:Stats.Errors += "Exception during upload: $_"
        Remove-Item $markerInProgress -ErrorAction SilentlyContinue
        Set-Content -Path $markerFailed -Value "Upload exception: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n$_"
        return $false
    }
}
#endregion

#region Main Execution
function Start-Ingest {
    param(
        [string]$DriveLetter,
        [string]$SourceDirectory,
        [bool]$MovFlags,
        [PSCustomObject]$Config,
        [bool]$UploadOnly,
        [bool]$SkipUpload,
        [bool]$Auto
    )

    Write-Log "=== Starting Video Ingest ===" -Level INFO

    $outputDir = $null

    # Handle Auto mode - detect source automatically
    if ($Auto) {
        Write-Log "=== Auto-Detection Mode ===" -Level INFO

        # Get camera config patterns
        $sdCardPattern = '^\d{3}CANON$'
        $deviceNamePattern = 'Canon'
        if ($Config.Camera) {
            if ($Config.Camera.SDCardFolderPattern) {
                $sdCardPattern = $Config.Camera.SDCardFolderPattern
            }
            if ($Config.Camera.DeviceNamePattern) {
                $deviceNamePattern = $Config.Camera.DeviceNamePattern
            }
        }

        # Try 1: SD card reader (fast filesystem access)
        $sdDriveLetter = Find-CanonSDCard -FolderPattern $sdCardPattern

        if ($sdDriveLetter) {
            Write-Log "Using SD card on drive ${sdDriveLetter}:" -Level SUCCESS
            $DriveLetter = $sdDriveLetter
            # Fall through to DriveLetter code path below
        }
        else {
            # Try 2: MTP USB connection (slower but works without card reader)
            Write-Log "No SD card found, trying MTP USB connection..." -Level INFO

            # Create output directory for MTP files
            $outputDir = New-OutputDirectory -OutputRoot $Config.OutputRoot `
                                             -IncludeTimestamp $Config.IncludeTimestampInFolder

            $copiedFiles = Copy-FromMTPDevice -DeviceNamePattern $deviceNamePattern `
                                               -DestinationPath $outputDir `
                                               -FileExtensions $Config.FileExtensions

            if (-not $copiedFiles -or $copiedFiles.Count -eq 0) {
                throw "No Canon video source found. Insert SD card into reader or connect camera via USB."
            }

            $Script:Stats.Found = $copiedFiles.Count

            # Check ffmpeg availability
            if (-not (Test-FfmpegAvailable -FfmpegPath $Config.FfmpegPath)) {
                throw "FFmpeg is not available. Please install ffmpeg and ensure it's in PATH or configure FfmpegPath in config.json"
            }

            # Process each copied file with Convert-VideoFile (which will smart-skip if not needed)
            # Since MTP copies land in the same output dir, remove the original after
            # successful conversion to avoid duplicates (original + _faststart copy)
            Write-Log "Processing $($copiedFiles.Count) file(s) from MTP device..." -Level INFO
            foreach ($file in $copiedFiles) {
                $result = Convert-VideoFile -SourceFile $file `
                                            -DestinationPath $outputDir `
                                            -Suffix $Config.AppendSuffix `
                                            -FfmpegPath $Config.FfmpegPath `
                                            -FfprobePath $Config.FfprobePath `
                                            -SkipIfExists $Config.SkipIfExists
                if ($result) {
                    $suffixedName = "$([System.IO.Path]::GetFileNameWithoutExtension($file.Name))$($Config.AppendSuffix)$($file.Extension)"
                    $suffixedPath = Join-Path $outputDir $suffixedName
                    # Only delete original if a separate suffixed file was created
                    if ((Test-Path $suffixedPath) -and $suffixedPath -ne $file.FullName) {
                        Remove-Item $file.FullName -Force
                        Write-Log "  Removed original (keeping processed copy): $($file.Name)" -Level INFO
                    }
                }
            }

            Write-Log "" -Level INFO
            Write-Log "=== MTP Ingest Complete ===" -Level SUCCESS
            # Skip to Phase 2 (rename) below
        }
    }

    # Handle SourceDirectory mode
    if ($SourceDirectory) {
        $sourcePath = Join-Path $Config.OutputRoot $SourceDirectory
        
        if (-not (Test-Path $sourcePath)) {
            throw "Source directory does not exist: $sourcePath"
        }
        
        Write-Log "=== Source Directory Mode ===" -Level INFO
        Write-Log "Source path: $sourcePath" -Level INFO
        
        if ($MovFlags) {
            # Process files with ffmpeg conversion
            Write-Log "MovFlags enabled - will process files with ffmpeg faststart" -Level INFO
            
            # Check ffmpeg
            if (-not (Test-FfmpegAvailable -FfmpegPath $Config.FfmpegPath)) {
                throw "FFmpeg is not available. Please install ffmpeg and ensure it's in PATH or configure FfmpegPath in config.json"
            }
            
            # Find video files in source directory
            $videoFiles = Find-VideoFiles -RootPath $sourcePath `
                                           -Extensions $Config.FileExtensions `
                                           -ExcludePaths $Config.ExcludePaths
            
            if ($videoFiles.Count -eq 0) {
                Write-Log "No video files found in $sourcePath" -Level WARN
                return
            }
            
            # Process each file (output to same directory)
            Write-Log "Beginning conversion of $($videoFiles.Count) file(s)..." -Level INFO
            
            foreach ($file in $videoFiles) {
                $result = Convert-VideoFile -SourceFile $file `
                                            -DestinationPath $sourcePath `
                                            -Suffix $Config.AppendSuffix `
                                            -FfmpegPath $Config.FfmpegPath `
                                            -FfprobePath $Config.FfprobePath `
                                            -SkipIfExists $Config.SkipIfExists
            }
            
            Write-Log "" -Level INFO
            Write-Log "=== Local Conversion Complete ===" -Level SUCCESS
        }
        else {
            Write-Log "MovFlags not specified - skipping ffmpeg conversion" -Level INFO
            
            # Count files for logging
            $existingFiles = Get-ChildItem -Path $sourcePath -File | 
                Where-Object { $_.Extension -match '\.mp4$' }
            Write-Log "Found $($existingFiles.Count) file(s) ready for upload" -Level INFO
        }
        
        $outputDir = $sourcePath
    }
    # Phase 1: Process files from drive (unless UploadOnly or already handled by Auto/MTP)
    elseif (-not $UploadOnly -and -not $outputDir) {
        Write-Log "=== Phase 1: Local Conversion ===" -Level INFO
        
        # Validate drive
        if (-not (Test-DriveExists -DriveLetter $DriveLetter)) {
            throw "Drive validation failed"
        }
        
        # Check ffmpeg
        if (-not (Test-FfmpegAvailable -FfmpegPath $Config.FfmpegPath)) {
            throw "FFmpeg is not available. Please install ffmpeg and ensure it's in PATH or configure FfmpegPath in config.json"
        }
        
        # Create output directory
        $outputDir = New-OutputDirectory -OutputRoot $Config.OutputRoot `
                                         -IncludeTimestamp $Config.IncludeTimestampInFolder
        
        # Find video files
        $drivePath = "${DriveLetter}:\"
        $videoFiles = Find-VideoFiles -RootPath $drivePath `
                                       -Extensions $Config.FileExtensions `
                                       -ExcludePaths $Config.ExcludePaths
    
        if ($videoFiles.Count -eq 0) {
            Write-Log "No video files found on drive $drivePath" -Level WARN
            return
        }
    
        # Process each file
        Write-Log "Beginning conversion of $($videoFiles.Count) file(s)..." -Level INFO
        
        foreach ($file in $videoFiles) {
            $result = Convert-VideoFile -SourceFile $file `
                                        -DestinationPath $outputDir `
                                        -Suffix $Config.AppendSuffix `
                                        -FfmpegPath $Config.FfmpegPath `
                                        -FfprobePath $Config.FfprobePath `
                                        -SkipIfExists $Config.SkipIfExists
        }
        
        Write-Log "" -Level INFO
        Write-Log "=== Local Conversion Complete ===" -Level SUCCESS
    }
    else {
        # UploadOnly mode - find most recent folder in pool directory
        Write-Log "=== Upload Only Mode ===" -Level INFO
        
        if (-not (Test-Path $Config.OutputRoot)) {
            throw "Output root directory does not exist: $($Config.OutputRoot)"
        }
        
        # Find all date-based folders (YYYY-MM-DD pattern)
        $dateFolders = Get-ChildItem -Path $Config.OutputRoot -Directory | 
            Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' } |
            Sort-Object Name -Descending
        
        if ($dateFolders.Count -eq 0) {
            throw "No date-based folders found in $($Config.OutputRoot). Run without -UploadOnly first to process files."
        }
        
        # Use the most recent folder
        $outputDir = $dateFolders[0].FullName
        Write-Log "Found most recent folder: $($dateFolders[0].Name)" -Level INFO
        Write-Log "Full path: $outputDir" -Level INFO
        
        # Count files (case-insensitive)
        $existingFiles = Get-ChildItem -Path $outputDir -File | 
            Where-Object { $_.Extension -match '\.mp4$' }
        Write-Log "Found $($existingFiles.Count) file(s) ready for upload" -Level INFO
    }
    
    # Phase 2: Extract metadata and rename files
    Write-Log "" -Level INFO
    $renameSuccess = Rename-VideosWithMetadata -DirectoryPath $outputDir -FfprobePath $Config.FfprobePath
    
    if (-not $renameSuccess) {
        throw "Metadata extraction/rename phase failed"
    }
    
    # Phase 3: Upload to Azure (unless SkipUpload)
    if (-not $SkipUpload -and $Config.AzCopy.Enabled) {
        Write-Log "" -Level INFO
        Write-Log "=== Phase 3: Azure Upload ===" -Level INFO
        
        # Check azcopy
        if (-not (Test-AzCopyAvailable -AzCopyPath $Config.AzCopy.AzCopyPath)) {
            Write-Log "AzCopy is not available. Skipping upload." -Level WARN
            Write-Log "Install AzCopy from: https://aka.ms/downloadazcopy" -Level WARN
        }
        else {
            $uploadSuccess = Start-AzureUpload -LocalPath $outputDir -AzCopyConfig $Config.AzCopy
            
            if ($uploadSuccess) {
                Write-Log "Azure upload completed successfully" -Level SUCCESS
            }
            else {
                Write-Log "Azure upload failed - see errors above" -Level ERROR
            }
        }
    }
    elseif ($SkipUpload) {
        Write-Log "" -Level INFO
        Write-Log "Skipping Azure upload (--SkipUpload specified)" -Level WARN
    }
    elseif (-not $Config.AzCopy.Enabled) {
        Write-Log "" -Level INFO
        Write-Log "Azure upload disabled in configuration" -Level WARN
    }
    
    # Summary
    Write-Log "" -Level INFO
    Write-Log "=== Process Complete ===" -Level SUCCESS
    Write-Log "Files found:     $($Script:Stats.Found)" -Level INFO
    Write-Log "Files processed: $($Script:Stats.Processed)" -Level SUCCESS
    Write-Log "Files skipped:   $($Script:Stats.Skipped)" -Level WARN
    Write-Log "Files failed:    $($Script:Stats.Failed)" -Level ERROR
    
    if ($Script:Stats.Errors.Count -gt 0) {
        Write-Log "" -Level INFO
        Write-Log "Errors encountered:" -Level ERROR
        foreach ($error in $Script:Stats.Errors) {
            Write-Log "  - $error" -Level ERROR
        }
    }
    
    Write-Log "" -Level INFO
    Write-Log "Output directory: $outputDir" -Level INFO
    Write-Log "Log file: $Script:LogFilePath" -Level INFO
}
#endregion

#region Script Entry Point
try {
    # Load configuration
    $config = Get-Config
    
    # Initialize logging
    Initialize-Logging -LogDirectory $config.LogDirectory
    
    # Start ingest
    Start-Ingest -DriveLetter $DriveLetter `
                 -SourceDirectory $SourceDirectory `
                 -MovFlags $MovFlags.IsPresent `
                 -Config $config `
                 -UploadOnly $UploadOnly.IsPresent `
                 -SkipUpload $SkipUpload.IsPresent `
                 -Auto $Auto.IsPresent
    
    exit 0
}
catch {
    Write-Log "Fatal error: $_" -Level ERROR
    Write-Log $_.ScriptStackTrace -Level ERROR
    exit 1
}
#endregion
