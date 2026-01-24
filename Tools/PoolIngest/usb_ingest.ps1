<#
.SYNOPSIS
    USB Video Ingest - Phase 1: Local conversion with ffmpeg faststart

.DESCRIPTION
    Scans a specified drive for MP4 files, processes them with ffmpeg to relocate
    the moov atom for streaming optimization, and organizes outputs into dated folders.

.PARAMETER DriveLetter
    Drive letter to scan (e.g., 'D' or 'E'). Not required when using -UploadOnly.

.PARAMETER UploadOnly
    Skip drive scanning and only upload existing files from the output directory.

.PARAMETER SkipUpload
    Skip Azure upload (process files only).

.PARAMETER WhatIf
    Dry-run mode. Shows what would be processed without making changes.

.EXAMPLE
    .\usb_ingest.ps1 -DriveLetter D
    Scans D:\ for MP4 files, processes them, and uploads to Azure.

.EXAMPLE
    .\usb_ingest.ps1 -DriveLetter E -WhatIf
    Shows what would be processed on E:\ without making changes.

.EXAMPLE
    .\usb_ingest.ps1 -UploadOnly
    Upload today's processed videos to Azure without scanning drives.

.EXAMPLE
    .\usb_ingest.ps1 -DriveLetter D -SkipUpload
    Process files from D:\ but don't upload to Azure.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory=$false)]
    [ValidatePattern('^[A-Za-z]$')]
    [string]$DriveLetter,

    [Parameter(Mandatory=$false)]
    [switch]$UploadOnly,

    [Parameter(Mandatory=$false)]
    [switch]$SkipUpload
)

# Script version
$ScriptVersion = "1.1.0-phase3"

# Validate parameter combinations
if ($UploadOnly -and $DriveLetter) {
    Write-Error "Cannot specify both -UploadOnly and -DriveLetter"
    exit 1
}

if (-not $UploadOnly -and -not $DriveLetter) {
    Write-Error "Must specify either -DriveLetter or -UploadOnly"
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
    
    Write-Log "=== Starting Azure Upload ==="  -Level INFO
    
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
    # Get the date folder name from the local path
    $folderName = Split-Path $LocalPath -Leaf
    
    # Build remote path components. AzCopy preserves the source folder name, so we only append the configured prefix.
    $remotePathParts = @()
    if ($AzCopyConfig.RemotePrefix) {
        $remotePathParts += $AzCopyConfig.RemotePrefix.Trim('/')
    }
    $remotePath = ($remotePathParts -join '/')

    Write-Log "Debug - Folder name: $folderName" -Level INFO
    Write-Log "Debug - Remote path: $remotePath" -Level INFO

    # Construct full destination URL: baseUrl/container[/remotePath]?SAS
    $baseUrl = $AzCopyConfig.BaseUrl.TrimEnd('/')
    $containerName = $AzCopyConfig.ContainerName.Trim('/')
    
    # Normalize SAS token - trim whitespace and remove leading ? if present
    # Do NOT decode or modify the signature - pass it verbatim
    $sasToken = $AzCopyConfig.SasToken.Trim()
    $sasToken = $sasToken.TrimStart('?')
    
    if ($remotePath) {
        $destinationUrl = "$baseUrl/$containerName/$remotePath`?$sasToken"
    }
    else {
        $destinationUrl = "$baseUrl/$containerName`?$sasToken"
    }
    
    Write-Log "Uploading from: $LocalPath" -Level INFO
    if ($remotePath) {
        Write-Log "Destination: $baseUrl/$containerName/$remotePath/$folderName" -Level INFO
    }
    else {
        Write-Log "Destination: $baseUrl/$containerName/$folderName" -Level INFO
    }
    
    # Get list of files matching the include pattern
    $includePatterns = if ($AzCopyConfig.IncludePattern) {
        $AzCopyConfig.IncludePattern -split ';'
    } else {
        @('*')
    }
    
    $filesToUpload = @()
    foreach ($pattern in $includePatterns) {
        $filesToUpload += Get-ChildItem -Path $LocalPath -Filter $pattern -File
    }
    
    # Deduplicate files (Windows is case-insensitive, so *.mp4 and *.MP4 match the same files)
    $filesToUpload = $filesToUpload | Sort-Object -Property FullName -Unique
    
    if ($filesToUpload.Count -eq 0) {
        Write-Log "No files found to upload in $LocalPath" -Level WARN
        return $false
    }
    
    Write-Log "Found $($filesToUpload.Count) file(s) to upload" -Level INFO
    
    # Build remote folder URL including the date folder
    if ($remotePath) {
        $remoteFolder = "$baseUrl/$containerName/$remotePath/$folderName"
        $remoteFolderForList = "$remotePath/$folderName"
    }
    else {
        $remoteFolder = "$baseUrl/$containerName/$folderName"
        $remoteFolderForList = $folderName
    }
    
    # Retry logic: up to 3 attempts
    $maxAttempts = 3
    $attempt = 1
    $allFilesUploaded = $false
    
    while ($attempt -le $maxAttempts -and -not $allFilesUploaded) {
        if ($attempt -gt 1) {
            Write-Log "" -Level INFO
            Write-Log "=== Upload Attempt $attempt of $maxAttempts ===" -Level WARN
        }
        
        # Check which files already exist remotely
        Write-Log "Checking remote files..." -Level INFO
        $remoteFiles = @{}
        
        try {
            $listUrl = "$baseUrl/$containerName/$remoteFolderForList`?$sasToken"
            $listOutput = & $AzCopyConfig.AzCopyPath list $listUrl 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                # Parse azcopy list output to find file names
                $listOutput | ForEach-Object {
                    if ($_ -match '; Content Length: ') {
                        # Extract filename from the line (format varies, but filename is typically at the start)
                        $line = $_.ToString()
                        $parts = $line -split ';'
                        if ($parts.Count -gt 0) {
                            $filename = ($parts[0].Trim() -split '/')[-1]
                            if ($filename) {
                                $remoteFiles[$filename] = $true
                            }
                        }
                    }
                }
                Write-Log "Found $($remoteFiles.Count) file(s) already in remote location" -Level INFO
            }
        }
        catch {
            Write-Log "Could not list remote files (will attempt all uploads): $_" -Level WARN
        }
        
        # Determine which files need uploading
        $filesToAttempt = @()
        foreach ($file in $filesToUpload) {
            if ($file.Name -and -not $remoteFiles.ContainsKey($file.Name)) {
                $filesToAttempt += $file
            }
            elseif (-not $file.Name) {
                Write-Log "Skipping file with null/empty name" -Level WARN
            }
        }
        
        if ($filesToAttempt.Count -eq 0) {
            Write-Log "All files already exist remotely!" -Level SUCCESS
            $allFilesUploaded = $true
            break
        }
        
        Write-Log "Need to upload $($filesToAttempt.Count) file(s)" -Level INFO
        
        $successCount = 0
        $failCount = 0
        
        foreach ($file in $filesToAttempt) {
            Write-Log "Uploading file: $($file.Name)..." -Level INFO
            
            # Build destination URL for this specific file
            $fileDestUrl = "$remoteFolder/$($file.Name)?$sasToken"
            
            $azcopyArgs = @(
                'copy',
                $file.FullName,
                $fileDestUrl
            )
            
            if ($AzCopyConfig.OverwritePolicy) {
                $azcopyArgs += "--overwrite=$($AzCopyConfig.OverwritePolicy)"
            }
            
            if ($WhatIfPreference) {
                Write-Log "[WhatIf] Would run: $($AzCopyConfig.AzCopyPath) $($azcopyArgs -join ' ')" -Level INFO
                $successCount++
                continue
            }
            
            try {
                # Run azcopy for this file
                & $AzCopyConfig.AzCopyPath @azcopyArgs 2>&1 | Out-Null
                $exitCode = $LASTEXITCODE
                
                if ($exitCode -eq 0) {
                    Write-Log "  ✓ Success: $($file.Name)" -Level SUCCESS
                    $successCount++
                }
                else {
                    Write-Log "  ✗ Failed: $($file.Name) (exit code $exitCode)" -Level ERROR
                    $failCount++
                    $Script:Stats.Errors += "Failed to upload $($file.Name): exit code $exitCode"
                }
            }
            catch {
                Write-Log "  ✗ Exception uploading $($file.Name): $_" -Level ERROR
                $failCount++
                $Script:Stats.Errors += "Exception uploading $($file.Name): $_"
            }
        }
        
        Write-Log "" -Level INFO
        Write-Log "Attempt $attempt summary: $successCount succeeded, $failCount failed" -Level INFO
        
        # Check if all files are now uploaded
        if ($failCount -eq 0) {
            $allFilesUploaded = $true
        }
        else {
            $attempt++
            if ($attempt -le $maxAttempts) {
                Write-Log "Waiting 5 seconds before retry..." -Level INFO
                Start-Sleep -Seconds 5
            }
        }
    }
    
    # Final verification
    if (-not $allFilesUploaded) {
        Write-Log "" -Level ERROR
        Write-Log "Upload incomplete after $maxAttempts attempts" -Level ERROR
        Remove-Item $markerInProgress -ErrorAction SilentlyContinue
        Set-Content -Path $markerFailed -Value "Upload incomplete after $maxAttempts attempts: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        return $false
    }
    else {
        Write-Log "" -Level SUCCESS
        Write-Log "All files uploaded successfully" -Level SUCCESS
        Remove-Item $markerInProgress -ErrorAction SilentlyContinue
        Set-Content -Path $markerComplete -Value "Upload completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`nAll files uploaded successfully"
        return $true
    }
}
#endregion

#region Main Execution
function Start-Ingest {
    param(
        [string]$DriveLetter,
        [PSCustomObject]$Config,
        [bool]$UploadOnly,
        [bool]$SkipUpload
    )
    
    Write-Log "=== Starting Video Ingest (Phase 3) ===" -Level INFO
    
    $outputDir = $null
    
    # Phase 1: Process files from drive (unless UploadOnly)
    if (-not $UploadOnly) {
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
                 -Config $config `
                 -UploadOnly $UploadOnly.IsPresent `
                 -SkipUpload $SkipUpload.IsPresent
    
    exit 0
}
catch {
    Write-Log "Fatal error: $_" -Level ERROR
    Write-Log $_.ScriptStackTrace -Level ERROR
    exit 1
}
#endregion
