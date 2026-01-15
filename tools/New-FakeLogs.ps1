#Requires -Version 7.0
<#
.SYNOPSIS
    Creates fake log files with realistic content.

.DESCRIPTION
    Generates fake log files with randomized timestamps and realistic log content.
    Each file contains at least 20 log lines with ISO 8601 timestamps and various log levels.

.PARAMETER Count
    Number of log files to create. Default: 10.

.PARAMETER OutputDirectory
    Directory to create the files in. Default: current working directory.

.EXAMPLE
    .\New-FakeLogs.ps1 -Count 5 -OutputDirectory "C:\Logs"
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$Count = 10,

    [Parameter()]
    [string]$OutputDirectory = $PWD.Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Create output directory if it doesn't exist
if (-not (Test-Path -Path $OutputDirectory)) {
    New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
}

# Character set for random string generation
$charSet = 'abcdefghijklmnopqrstuvwxyz0123456789'

function Get-RandomString {
    param([int]$Length = 5)
    $result = -join (1..$Length | ForEach-Object { $charSet[(Get-Random -Maximum $charSet.Length)] })
    return $result
}

function Get-RandomTimestamp {
    $today = Get-Date -Format 'yyyy.MM.dd'
    $hour = Get-Random -Minimum 0 -Maximum 24
    $minute = Get-Random -Minimum 0 -Maximum 60
    $second = Get-Random -Minimum 0 -Maximum 60
    $milliseconds = Get-Random -Minimum 0 -Maximum 10000
    return "{0}.{1:D2}.{2:D2}.{3:D2}.{4:D4}" -f $today, $hour, $minute, $second, $milliseconds
}

function Get-UniqueFileName {
    param([string]$Directory)
    
    $maxAttempts = 100
    $attempt = 0
    
    do {
        $timestamp = Get-RandomTimestamp
        $randomSuffix = Get-RandomString -Length 5
        $fileName = "{0}-{1}.log" -f $timestamp, $randomSuffix
        $fullPath = Join-Path -Path $Directory -ChildPath $fileName
        $attempt++
    } while ((Test-Path -Path $fullPath) -and ($attempt -lt $maxAttempts))
    
    if ($attempt -ge $maxAttempts) {
        throw "Unable to generate unique filename after $maxAttempts attempts"
    }
    
    return @{
        FileName = $fileName
        FullPath = $fullPath
    }
}

function Get-FakeLogContent {
    $messages = @(
        @{ Level = 'INFO'; Message = 'Application started successfully' }
        @{ Level = 'INFO'; Message = 'Configuration loaded from /etc/app/config.json' }
        @{ Level = 'INFO'; Message = 'Database connection established to postgresql://db.internal:5432/appdb' }
        @{ Level = 'DEBUG'; Message = 'Processing request with correlation ID: {0}' }
        @{ Level = 'INFO'; Message = 'User authentication successful for user_id={0}' }
        @{ Level = 'WARN'; Message = 'Slow query detected (elapsed: {0}ms) on /api/v2/users' }
        @{ Level = 'DEBUG'; Message = 'Cache hit for key: session_{0}' }
        @{ Level = 'DEBUG'; Message = 'Cache miss for key: user_profile_{0}' }
        @{ Level = 'INFO'; Message = 'HTTP GET /api/v1/health returned 200 OK' }
        @{ Level = 'INFO'; Message = 'HTTP POST /api/v2/orders processed in {0}ms' }
        @{ Level = 'WARN'; Message = 'Rate limit approaching for client IP 192.168.{0}.{1}' }
        @{ Level = 'ERROR'; Message = 'Failed to connect to external service at https://api.external.com/v1/validate' }
        @{ Level = 'INFO'; Message = 'Background job scheduler initialized with {0} workers' }
        @{ Level = 'DEBUG'; Message = 'Memory usage: {0}MB / 2048MB' }
        @{ Level = 'INFO'; Message = 'Message published to queue: orders.created' }
        @{ Level = 'INFO'; Message = 'Message consumed from queue: payments.pending' }
        @{ Level = 'WARN'; Message = 'Retry attempt {0}/3 for transaction_id={1}' }
        @{ Level = 'ERROR'; Message = 'Timeout exceeded while waiting for response from upstream service' }
        @{ Level = 'INFO'; Message = 'File uploaded successfully: /uploads/{0}/document.pdf' }
        @{ Level = 'DEBUG'; Message = 'Session token refreshed for session_id={0}' }
        @{ Level = 'INFO'; Message = 'Scheduled task completed: cleanup_expired_sessions' }
        @{ Level = 'WARN'; Message = 'Disk usage at {0}% on volume /data' }
        @{ Level = 'INFO'; Message = 'WebSocket connection established from client {0}' }
        @{ Level = 'DEBUG'; Message = 'Validating request payload against schema v2.1' }
        @{ Level = 'INFO'; Message = 'Feature flag evaluated: dark_mode_enabled = true for user {0}' }
        @{ Level = 'ERROR'; Message = 'Database query failed: connection reset by peer' }
        @{ Level = 'INFO'; Message = 'Health check passed: all services operational' }
        @{ Level = 'DEBUG'; Message = 'Request headers: Content-Type=application/json, Accept=*/*' }
        @{ Level = 'WARN'; Message = 'Deprecated API endpoint called: /api/v1/legacy/users' }
        @{ Level = 'INFO'; Message = 'Audit log entry created for action: user.login' }
    )
    
    $lines = @()
    $lineCount = Get-Random -Minimum 20 -Maximum 35
    $baseTime = Get-Date
    
    for ($i = 0; $i -lt $lineCount; $i++) {
        # Generate incrementing timestamps for log lines
        $logTime = $baseTime.AddMilliseconds($i * (Get-Random -Minimum 50 -Maximum 500))
        $isoTimestamp = $logTime.ToString('yyyy-MM-ddTHH:mm:ss.fffK')
        
        $msgTemplate = $messages | Get-Random
        $level = $msgTemplate.Level
        $message = $msgTemplate.Message
        
        # Replace placeholders with random values
        if ($message -match '\{0\}') {
            $randomValue = switch -Regex ($message) {
                'correlation ID' { [guid]::NewGuid().ToString().Substring(0, 8) }
                'user_id|user |session_id|session_' { Get-Random -Minimum 1000 -Maximum 99999 }
                'elapsed|processed in' { Get-Random -Minimum 50 -Maximum 3000 }
                'workers' { Get-Random -Minimum 2 -Maximum 16 }
                'Memory usage' { Get-Random -Minimum 256 -Maximum 1800 }
                'Retry attempt' { Get-Random -Minimum 1 -Maximum 3 }
                'Disk usage' { Get-Random -Minimum 70 -Maximum 95 }
                'client' { Get-RandomString -Length 8 }
                'uploads' { Get-RandomString -Length 12 }
                default { Get-Random -Minimum 1 -Maximum 255 }
            }
            $message = $message -replace '\{0\}', $randomValue
        }
        if ($message -match '\{1\}') {
            $randomValue2 = Get-Random -Minimum 1 -Maximum 255
            $message = $message -replace '\{1\}', $randomValue2
        }
        
        $lines += "[$isoTimestamp] [$level] $message"
    }
    
    return $lines -join "`n"
}

# Main execution
$createdFiles = @()

for ($i = 0; $i -lt $Count; $i++) {
    $fileInfo = Get-UniqueFileName -Directory $OutputDirectory
    $content = Get-FakeLogContent
    Set-Content -Path $fileInfo.FullPath -Value $content -Encoding UTF8
    $createdFiles += $fileInfo.FileName
}

# Output summary
Write-Host "`n===== Log File Generation Summary =====" -ForegroundColor Cyan
Write-Host "Directory: $OutputDirectory" -ForegroundColor Green
Write-Host "Files created: $($createdFiles.Count)" -ForegroundColor Green
Write-Host "`nSample filenames:" -ForegroundColor Yellow
$sampleCount = [Math]::Min(5, $createdFiles.Count)
$createdFiles | Select-Object -First $sampleCount | ForEach-Object {
    Write-Host "  - $_" -ForegroundColor White
}
if ($createdFiles.Count -gt $sampleCount) {
    Write-Host "  ... and $($createdFiles.Count - $sampleCount) more" -ForegroundColor Gray
}
Write-Host "========================================`n" -ForegroundColor Cyan
