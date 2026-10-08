<#
.SYNOPSIS
    Downloads and updates the Microsoft TSS (TroubleshootingScript toolset).

.DESCRIPTION
    Checks for the existence of the TSS toolset in the specified Tools directory.
    If missing, it downloads the latest version from https://aka.ms/GetTSS and extracts it.

.PARAMETER ToolsPath
    The directory where TSS should be installed. Defaults to "..\Tools" relative to the script.
#>
param(
    [string]$ToolsPath = "$PSScriptRoot\..\Tools"
)

$TssUrl = "https://aka.ms/GetTSS"
$ZipPath = Join-Path $ToolsPath "TSS.zip"
$ExtractPath = Join-Path $ToolsPath "TSS"

# Ensure Tools directory exists
if (!(Test-Path $ToolsPath)) {
    Write-Host "Creating Tools directory: $ToolsPath"
    New-Item -ItemType Directory -Path $ToolsPath -Force | Out-Null
}

# Function to Download and Extract TSS
function Install-TSS {
    Write-Host "Downloading TSS from $TssUrl..."
    try {
        Invoke-WebRequest -Uri $TssUrl -OutFile $ZipPath -UseBasicParsing
    }
    catch {
        Write-Error "Failed to download TSS: $_"
        return
    }

    Write-Host "Extracting TSS to $ExtractPath..."
    try {
        # Force overwrite if exists updates
        Expand-Archive -Path $ZipPath -DestinationPath $ExtractPath -Force
    }
    catch {
        Write-Error "Failed to extract TSS: $_"
        return
    }

    # Cleanup
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }
    Write-Host "TSS successfully installed/updated."
}

# Check if TSS exists
if (Test-Path $ExtractPath) {
    Write-Host "TSS found at $ExtractPath."
    # TODO: Implement version/date check to force update if old?
    # For now, we assume if it's there, it's good, unless user forces (can add -Force switch later)
}
else {
    Install-TSS
}
