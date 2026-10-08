<#
.SYNOPSIS
    Runs the Windows Update Troubleshooter silently and reports results.

.DESCRIPTION
    This script uses Invoke-TroubleshootingPack to run the Windows Update diagnostics
    in Unattended mode. It saves the results to C:\MS_DATA, parses the result XML,
    and outputs key findings to the console.

.PARAMETER Verbose
    If specified, lists the specific components (Services, DLLs, Data Stores) 
    that are checked and repaired by the troubleshooting pack.

.EXAMPLE
    .\Invoke-WUTroubleshooter.ps1
    .\Invoke-WUTroubleshooter.ps1 -Verbose
#>
[CmdletBinding()]
Param()

$PackPath = "C:\Windows\Diagnostics\System\WindowsUpdate"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ResultDir = "C:\MS_DATA\WUTroubleshooter_$Timestamp"

# --- Verbose Reporting of Checks/Repairs ---
if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"]) {
    Write-Host "`n--- Windows Update Troubleshooter Analysis ---" -ForegroundColor Cyan
    Write-Host "The following components are checked and repaired by this tool:" -ForegroundColor Gray
    
    Write-Host "`n[Services]" -ForegroundColor Yellow
    Write-Host "- wuauserv (Windows Update)"
    Write-Host "- bits (Background Intelligent Transfer Service)"
    Write-Host "- CryptSvc (Cryptographic Services)"
    
    Write-Host "`n[DLL Registration]" -ForegroundColor Yellow
    Write-Host "Re-registers 35 core DLLs including:"
    Write-Host "- Core: atl.dll, mshtml.dll, jscript.dll, vbscript.dll, msxml*.dll"
    Write-Host "- Crypto: wintrust.dll, dssenh.dll, rsaenh.dll, cryptdlg.dll"
    Write-Host "- WU/Net: wuapi.dll, wuaueng.dll, qmgr.dll, shell32.dll"

    Write-Host "`n[Data Stores & Network]" -ForegroundColor Yellow
    Write-Host "- BITS Queue: Clears stuck jobs (qmgr*.dat)"
    Write-Host "- Network: Resets WinHTTP proxy configuration"
    Write-Host "------------------------------------------------`n"
}

# Check for Administrator privileges
$currentPrincipal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This script is NOT running as Administrator."
    Write-Warning "Diagnostic checks may list 'Not Checked' without elevated privileges."
    Write-Warning "Please run this script from an elevated PowerShell prompt."
}

# Ensure output directory exists
if (-not (Test-Path "C:\MS_DATA")) {
    New-Item -Path "C:\MS_DATA" -ItemType Directory | Out-Null
}

Write-Host "Starting Windows Update Troubleshooter (Silent)..." -ForegroundColor Cyan
Write-Host "This may take a few minutes." -ForegroundColor Cyan

try {
    # Get the pack and invoke it unattended
    $Pack = Get-TroubleshootingPack -Path $PackPath
    Invoke-TroubleshootingPack -Pack $Pack -Result $ResultDir -Unattended | Out-Null
    
    Write-Host "Troubleshooting completed." -ForegroundColor Green
    Write-Host "Results saved to: $ResultDir" -ForegroundColor Gray

    # Parse Results
    $ReportPath = Join-Path $ResultDir "ResultReport.xml"
    if (Test-Path $ReportPath) {
        [xml]$Xml = Get-Content $ReportPath

        Write-Host "`n--- Publisher Details ---" -ForegroundColor Yellow
        $PkgInfo = $Xml.ResultReport.Header.PackageInformation
        $Publisher = ($PkgInfo.Data | Where-Object { $_.name -eq "Publisher" })."#text"
        $Version = ($PkgInfo.Data | Where-Object { $_.name -eq "Package Version" })."#text"
        Write-Host "Publisher: $Publisher"
        Write-Host "Version: $Version"

        Write-Host "`n--- Potential Issues Checked ---" -ForegroundColor Yellow
        
        # Navigate to RootCause elements
        $RootCauses = $Xml.ResultReport.Package.Problem.RootCauseInformation.RootCause
        
        if ($RootCauses) {
            foreach ($RC in $RootCauses) {
                $IssueName = $RC.name
                # Status is a standard Data element with name="Status"
                $Status = ($RC.Data | Where-Object { $_.name -eq "Status" })."#text"
                
                # Determine color based on status
                $Color = "Gray"
                if ($Status -eq "Detected") { $Color = "Red" }
                elseif ($Status -eq "Not Detected") { $Color = "Green" }
                
                Write-Host "$IssueName : $Status" -ForegroundColor $Color
            }
        }
        else {
            Write-Host "No checks found in report."
        }
    }
    else {
        Write-Warning "ResultReport.xml not found in $ResultDir"
    }
}
catch {
    Write-Error "Failed to run troubleshooter: $_"
}
