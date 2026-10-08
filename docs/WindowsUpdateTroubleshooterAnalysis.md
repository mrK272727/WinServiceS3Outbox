# Windows Update Troubleshooting Pack Analysis

## Overview
This document details the internal logic of the Windows Update Troubleshooting Pack (`WindowsUpdateDiagnostic.diagpkg`), based on analysis of its PowerShell scripts.

**Location:** `C:\Windows\Diagnostics\System\WindowsUpdate`

## Key Scripts

### 1. TS_Main.ps1 (Orchestration)
This is the main entry point for the troubleshooting pack.
- **Service Checks:** verifies `wuauserv` (Windows Update) is running.
- **Network Check:** Checks for internet connectivity.
- **Pending Restart:** Checks if a reboot is pending (`RC_PendingRestart.ps1`).
- **Data Store & Registry:** Invokes `RC_DataStore.ps1` and `RC_AppData.ps1` to check/repair data locations.
- **Error Codes:** Aggregates error codes and calls `RC_GenWUError.ps1`.

### 2. RS_GENWUError.ps1 (Repair Logic)
This script handles the heavy lifting of repairing Windows Update components.

#### Services
Attempts to stop, reset, and restart:
- **wuauserv**: Windows Update
- **bits**: Background Intelligent Transfer Service
- **CryptSvc**: Cryptographic Services

#### DLL Registration
Re-registers the following 35 DLLs to ensure COM interfaces are intact:
**Core Components:**
- `atl.dll`, `urlmon.dll`, `mshtml.dll`, `shdocvw.dll`, `browseui.dll`
- `jscript.dll`, `vbscript.dll`, `scrrun.dll`
- `msxml.dll`, `msxml3.dll`, `msxml6.dll`, `actxprxy.dll`

**Security & Crypto:**
- `softpub.dll`, `wintrust.dll`, `dssenh.dll`, `rsaenh.dll`
- `gpkcsp.dll`, `sccbase.dll`, `slbcsp.dll`, `cryptdlg.dll`, `initpki.dll`

**Windows Update & Networking:**
- `oleaut32.dll`, `ole32.dll`, `shell32.dll`
- `wuapi.dll`, `wuaueng.dll`, `wuaueng1.dll`, `wucltui.dll`
- `wups.dll`, `wups2.dll`, `wuweb.dll`
- `qmgr.dll`, `qmgrprxy.dll`, `wucltux.dll`
- `muweb.dll`, `wuwebv.dll`

#### Data Stores
- **BITS Queue:** Deletes `qmgr*.dat` from `%ALLUSERSPROFILE%\Application Data\Microsoft\Network\Downloader` to clear stuck BITS jobs.

#### Network
- **Proxy:** Resets WinHTTP proxy using `netsh winhttp reset proxy`.
