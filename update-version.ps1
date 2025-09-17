#!/usr/bin/env pwsh
# Script to update the cache-busting version parameter in all HTML files
# Usage: ./update-version.ps1 [version]
# If no version is provided, it will use current timestamp

param(
    [string]$Version
)

# Generate version if not provided
if (-not $Version) {
    $Version = Get-Date -Format "yyyyMMddHHmm"
}

Write-Host "Updating auth.js version to: $Version"

# Files to update
$files = @(
    "docs/app.html",
    "docs/schedule-new.html", 
    "docs/lineup-explorer-new.html",
    "docs/login.html",
    "docs/team-admin.html"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "Updating $file..."
        $content = Get-Content $file -Raw
        $content = $content -replace 'auth\.js\?v=\d+', "auth.js?v=$Version"
        $content | Set-Content $file -NoNewline
    } else {
        Write-Warning "File not found: $file"
    }
}

Write-Host "Version update complete!"
Write-Host "Updated files to use: /assets/auth.js?v=$Version"