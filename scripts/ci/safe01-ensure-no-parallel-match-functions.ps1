<#!
.SYNOPSIS
  SAFE01 safeguard: Prevent introduction of parallel match functions classes.
.DESCRIPTION
  Scans the repository for any *MatchesFunctions.cs files other than the canonical functions/league/MatchesFunctions.cs.
  If additional files are detected (e.g., CaptainMatchesFunctions.cs, TeamMatchesFunctions.cs), the script exits 1.
.USAGE
  pwsh ./scripts/ci/safe01-ensure-no-parallel-match-functions.ps1
.NOTES
  Integrate into CI before build/test steps. Constitution Ref: v1.0.0 (Reuse & Cohesion Principle)
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$canonical = Join-Path $repoRoot 'functions' 'league' 'MatchesFunctions.cs'

if (-not (Test-Path $canonical)) {
  Write-Error "Canonical MatchesFunctions.cs not found at expected path: $canonical"
  exit 2
}

# Find any *MatchesFunctions.cs besides the canonical (case-insensitive)
$all = Get-ChildItem -Path $repoRoot -Recurse -Filter '*MatchesFunctions.cs' | Where-Object { $_.FullName -ne $canonical }

if ($all.Count -gt 0) {
  Write-Host '❌ SAFE01 Violation: Additional MatchesFunctions-like classes detected:' -ForegroundColor Red
  $all | ForEach-Object { Write-Host " - $($_.FullName)" -ForegroundColor Red }
  Write-Host 'This violates the Cohesion / Single Extension Point rule. Remove or merge these implementations into the canonical MatchesFunctions.cs.' -ForegroundColor Red
  exit 1
}

# Also check for suspicious captain-specific placeholder names even if they don't end with MatchesFunctions.cs
$suspicious = Get-ChildItem -Path $repoRoot -Recurse -Include 'CaptainMatchesFunctions.cs','TeamMatchesFunctions.cs','Captain*Functions.cs','*CaptainMatches*.cs' -ErrorAction SilentlyContinue
if ($suspicious) {
  Write-Host '❌ SAFE01 Warning: Suspicious captain-related function files detected:' -ForegroundColor Yellow
  $suspicious | ForEach-Object { Write-Host " - $($_.FullName)" -ForegroundColor Yellow }
  Write-Host 'If these are new function entry points duplicating match logic, remove them.' -ForegroundColor Yellow
  exit 1
}

Write-Host '✅ SAFE01 Passed: No parallel match functions classes found.' -ForegroundColor Green
exit 0
