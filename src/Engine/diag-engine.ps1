# System Diagnostic engine (PowerShell 5.1) for WinPE Launcher-NG.
# Collects a lean system overview with Get-CimInstance and writes tab-separated
# rows (Section<TAB>Key<TAB>Value) as UTF-8 to -OutFile. Each section is guarded
# so a missing CIM class on WinPE never aborts the whole run.
param(
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = 'SilentlyContinue'

$rows = New-Object System.Collections.Generic.List[string]

function Add-Row([string]$section, [string]$key, $value) {
    if ($null -eq $value) { return }
    $val = ([string]$value).Trim()
    if ($val.Length -eq 0) { $val = '-' }
    $val = $val -replace "\r", ' ' -replace "\n", ' '
    $rows.Add(($section + "`t" + $key + "`t" + $val))
}

function GB($bytes) {
    if ($null -eq $bytes) { return $null }
    try { return ('{0:N1} GB' -f ([double]$bytes / 1GB)) } catch { return $null }
}

function Device-ErrorText($code) {
    switch ([int]$code) {
        1  { 'Device not configured correctly' }
        3  { 'Driver corrupted or out of memory' }
        10 { 'Device cannot start' }
        12 { 'Not enough resources' }
        14 { 'Restart required' }
        18 { 'Reinstall the driver' }
        19 { 'Registry problem' }
        21 { 'Windows is removing the device' }
        22 { 'Device is disabled' }
        24 { 'Device not present or not working' }
        28 { 'Driver not installed / not loaded' }
        31 { 'Windows cannot load the driver' }
        43 { 'Driver stopped: it reported a problem' }
        45 { 'Device not currently connected' }
        default { 'Device error' }
    }
}

function Try-Section([string]$name, [scriptblock]$body) {
    try { & $body } catch { Add-Row $name 'Error' $_.Exception.Message }
}

# --- System ---------------------------------------------------------------
Try-Section 'System' {
    $cs = Get-CimInstance Win32_ComputerSystem
    $csp = Get-CimInstance Win32_ComputerSystemProduct
    $bios = Get-CimInstance Win32_BIOS
    $bb = Get-CimInstance Win32_BaseBoard
    $se = Get-CimInstance Win32_SystemEnclosure
    Add-Row 'System' 'Manufacturer' $cs.Manufacturer
    Add-Row 'System' 'Model' $cs.Model
    Add-Row 'System' 'Serial number' $csp.IdentifyingNumber
    Add-Row 'System' 'System type' $cs.SystemType
    Add-Row 'System' 'Chassis' (($se.ChassisTypes | ForEach-Object { $_.ToString() }) -join ', ')
    Add-Row 'System' 'BIOS' (($bios.Manufacturer + ' ' + $bios.SMBIOSBIOSVersion).Trim())
    Add-Row 'System' 'BIOS date' $bios.ReleaseDate
    Add-Row 'System' 'Baseboard' (($bb.Manufacturer + ' ' + $bb.Product).Trim())
}

# --- CPU ------------------------------------------------------------------
Try-Section 'CPU' {
    $procs = Get-CimInstance Win32_Processor
    $cpu = $procs | Select-Object -First 1
    Add-Row 'CPU' 'Name' $cpu.Name
    $cores = ($procs | Measure-Object -Property NumberOfCores -Sum).Sum
    $threads = ($procs | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
    $sockets = @($procs).Count
    if ($cores) { Add-Row 'CPU' 'Cores' $cores }
    if ($threads) { Add-Row 'CPU' 'Threads' $threads }
    if ($sockets -gt 1) { Add-Row 'CPU' 'Sockets' $sockets }
    if ($cpu.MaxClockSpeed) { Add-Row 'CPU' 'Max clock' ("" + $cpu.MaxClockSpeed + ' MHz') }
    if ($cpu.LoadPercentage -ne $null) { Add-Row 'CPU' 'Current load' ("" + $cpu.LoadPercentage + '%') }
}

# --- Memory ---------------------------------------------------------------
Try-Section 'Memory' {
    $os = Get-CimInstance Win32_OperatingSystem
    if ($os.TotalVisibleMemorySize) { Add-Row 'Memory' 'Total RAM' (GB ([double]$os.TotalVisibleMemorySize * 1KB)) }
    if ($os.FreePhysicalMemory) { Add-Row 'Memory' 'Free RAM' (GB ([double]$os.FreePhysicalMemory * 1KB)) }
    Get-CimInstance Win32_PhysicalMemory | ForEach-Object {
        $loc = (($_.DeviceLocator, $_.BankLabel) | Where-Object { $_ }) -join ' / '
        if (-not $loc) { $loc = 'Module' }
        $bits = @()
        if ($_.Capacity) { $bits += (GB $_.Capacity) }
        if ($_.Speed) { $bits += ("" + $_.Speed + ' MT/s') }
        $val = ($bits -join ' @ ')
        $mk = (($_.Manufacturer, $_.PartNumber) | Where-Object { $_ }) -join ' '
        if ($mk) { if ($val) { $val += ' - ' }; $val += $mk }
        Add-Row 'Memory' $loc $val
    }
}

# --- Storage --------------------------------------------------------------
Try-Section 'Storage' {
    $pd = Get-CimInstance -Namespace root/Microsoft/Windows/Storage -ClassName MSFT_PhysicalDisk
    if ($pd) {
        foreach ($d in $pd) {
            $health = switch ([int]$d.HealthStatus) { 0 { 'Healthy' } 1 { 'Warning' } 2 { 'Unhealthy' } default { 'Unknown' } }
            $bus = switch ([int]$d.BusType) { 17 { 'NVMe' } 11 { 'SATA' } 8 { 'RAID' } 7 { 'USB' } default { [string]$d.BusType } }
            $media = switch ([int]$d.MediaType) { 3 { 'HDD' } 4 { 'SSD' } 5 { 'SCM' } default { 'Unspecified' } }
            Add-Row 'Storage' ($d.FriendlyName) (("Size=" + (GB $d.Size)) + ' Bus=' + $bus + ' Media=' + $media + ' Health=' + $health)
        }
    }
    else {
        Get-CimInstance Win32_DiskDrive | ForEach-Object {
            Add-Row 'Storage' ($_.Model) (("Size=" + (GB $_.Size)) + ' Interface=' + $_.InterfaceType + ' Status=' + $_.Status)
        }
    }
    Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 -or $_.DriveType -eq 2 } | ForEach-Object {
        $type = if ($_.DriveType -eq 3) { 'Fixed' } else { 'Removable' }
        $label = if ($_.VolumeName) { ' "' + $_.VolumeName + '"' } else { '' }
        Add-Row 'Volumes' ($_.DeviceID + $label) ('[' + $type + '] ' + $_.FileSystem + ' Size=' + (GB $_.Size) + ' Free=' + (GB $_.FreeSpace))
    }
}

# --- Graphics -------------------------------------------------------------
Try-Section 'Graphics' {
    Get-CimInstance Win32_VideoController | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_.Name)) { return }
        Add-Row 'Graphics' $_.Name ('Driver ' + $_.DriverVersion)
    }
}

# --- Network --------------------------------------------------------------
Try-Section 'Network' {
    Get-CimInstance Win32_NetworkAdapterConfiguration | Where-Object { $_.IPEnabled -eq $true } | ForEach-Object {
        $desc = if ($_.Description) { $_.Description } else { $_.MACAddress }
        Add-Row 'Network' $desc `
            ('IP=' + (($_.IPAddress) -join ', ') + '  GW=' + (($_.DefaultIPGateway) -join ', ') + '  DNS=' + (($_.DNSServerSearchOrder) -join ', '))
    }
}

# --- Battery --------------------------------------------------------------
Try-Section 'Battery' {
    $bat = Get-CimInstance Win32_Battery
    if ($bat) {
        foreach ($b in $bat) {
            Add-Row 'Battery' $b.Name ("" + $b.EstimatedChargeRemaining + '% (Status ' + $b.BatteryStatus + ')')
        }
    }
    else {
        Add-Row 'Battery' 'Present' 'No'
    }
}

# --- Problem devices ------------------------------------------------------
Try-Section 'Problem devices' {
    $bad = Get-CimInstance Win32_PnPEntity | Where-Object { $_.ConfigManagerErrorCode -ne 0 }
    if ($bad) {
        foreach ($p in ($bad | Select-Object -First 25)) {
            Add-Row 'Problem devices' $p.Name ((Device-ErrorText $p.ConfigManagerErrorCode) + ' (code ' + $p.ConfigManagerErrorCode + ')')
        }
    }
    else {
        Add-Row 'Problem devices' 'Status' 'None'
    }
}

# --- Write UTF-8 (no BOM) -------------------------------------------------
try {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllLines($OutFile, $rows.ToArray(), $enc)
}
catch {
    try { $rows | Out-File -LiteralPath $OutFile -Encoding utf8 } catch { }
}
