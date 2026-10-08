<#
.SYNOPSIS
    Runs TSS with DND_WU profile, filters logs, and gets last Security Update.
#>

$TssPath = Join-Path "$PSScriptRoot\..\Tools\TSS" "TSS.ps1"
$TssPath = $TssPath -replace '\\', '\' # Normalize slashes just in case

Write-Host "TSS Path: $TssPath"

if (! (Test-Path $TssPath)) {
    Write-Error "TSS.ps1 not found at $TssPath"
    exit 1
}

Write-Host "Starting TSS with DND_WU profile..."
# Using StartNoWait to return control
# We assume TSS is robust enough.

& "$TssPath" -DND_WU -StartNoWait

Write-Host "Waiting for 30 seconds for logs to collect..."
Start-Sleep -Seconds 30

Write-Host "Stopping TSS..."
& $TssPath -Stop

# Find the latest log folder in C:\MS_DATA
$MsDataPath = "C:\MS_DATA"
if (Test-Path $MsDataPath) {
    $LatestLogDir = Get-ChildItem $MsDataPath -Directory | Sort-Object CreationTime -Descending | Select-Object -First 1
    
    if ($LatestLogDir) {
        Write-Host "Latest Log Directory: $($LatestLogDir.FullName)"
        
        # Look for WindowsUpdate.log or relevant text logs
        # TSS might produce *WindowsUpdate*.log
        $LogFiles = Get-ChildItem $LatestLogDir.FullName -Filter "*WindowsUpdate*.log" -Recurse
        
        if (! $LogFiles) {
            Write-Warning "No WindowsUpdate.log found in $($LatestLogDir.FullName). Checking for other text logs..."
            $LogFiles = Get-ChildItem $LatestLogDir.FullName -Filter "*.log" -Recurse
        }

        if ($LogFiles) {
            foreach ($LogFile in $LogFiles) {
                Write-Host "Scanning $($LogFile.Name)..."
                $Errors = Select-String -Path $LogFile.FullName -Pattern "Error", "Failed", "0x8" -SimpleMatch:$false
                if ($Errors) {
                    Write-Host "Found $($Errors.Count) potential errors in $($LogFile.Name):" -ForegroundColor Red
                    $Errors | Select-Object -First 10 | ForEach-Object { Write-Host $_.Line.Trim() }
                    if ($Errors.Count -gt 10) { Write-Host "... and $($Errors.Count - 10) more." }
                }
                else {
                    Write-Host "No obvious errors found in $($LogFile.Name)." -ForegroundColor Green
                }
            }
        }
        else {
            Write-Error "No log files found to analyze."
        }
    }
    else {
        Write-Error "No log directory found in $MsDataPath."
    }
}
else {
    Write-Error "$MsDataPath does not exist."
}

Write-Host "`nGetting last installed Security Update KB..."
$SecurityUpdate = Get-HotFix | Where-Object { $_.Description -match "Security Update" } | Sort-Object InstalledOn -Descending | Select-Object -First 1

if ($SecurityUpdate) {
    Write-Host "Last Security Update: $($SecurityUpdate.HotFixID) installed on $($SecurityUpdate.InstalledOn)" -ForegroundColor Cyan
}
else {
    Write-Warning "No Security Updates found."
}
