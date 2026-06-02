param(
    [ValidateSet("https", "http")]
    [string]$WebApiScheme = "https",

    [int]$WebApiHttpsPort = 44312,

    [int]$WebApiHttpPort = 5099,

    [string]$NgrokDomain,

    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

function Test-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-TcpEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutMilliseconds = 1000
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connectTask = $client.ConnectAsync($HostName, $Port)
        return $connectTask.Wait($TimeoutMilliseconds) -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-ForTcpEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-TcpEndpoint -HostName $HostName -Port $Port) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for TCP endpoint ${HostName}:$Port"
}

function Test-DockerDaemon {
    if (-not (Test-Command -Name "docker")) {
        return $false
    }

    & docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

function Get-DockerDesktopPath {
    $candidatePaths = @(
        (Join-Path $env:ProgramFiles "Docker/Docker/Docker Desktop.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Docker/Docker/Docker Desktop.exe"),
        (Join-Path $env:LOCALAPPDATA "Docker/Docker Desktop.exe")
    )

    foreach ($path in $candidatePaths) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }

    return $null
}

function Wait-ForDockerDaemon {
    param([int]$TimeoutSeconds = 120)

    if (Test-DockerDaemon) {
        return
    }

    $dockerDesktopPath = Get-DockerDesktopPath
    if ([string]::IsNullOrWhiteSpace($dockerDesktopPath)) {
        throw "Docker daemon is not running and Docker Desktop was not found. Start Docker Desktop or start RabbitMQ manually on 127.0.0.1:5672, then rerun this script."
    }

    Write-Host "Docker daemon is not running. Starting Docker Desktop ..."
    Start-Process -FilePath $dockerDesktopPath -WindowStyle Hidden | Out-Null

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-DockerDaemon) {
            return
        }

        Start-Sleep -Seconds 3
    }

    throw "Docker Desktop was started, but Docker daemon was not ready within $TimeoutSeconds seconds. Open Docker Desktop until it finishes starting, then rerun this script."
}

function Ensure-LocalRabbitMq {
    if (Test-TcpEndpoint -HostName "127.0.0.1" -Port 5672) {
        return
    }

    if (-not (Test-Command -Name "docker")) {
        throw "RabbitMQ is not reachable at 127.0.0.1:5672 and Docker CLI was not found. Start RabbitMQ or install Docker."
    }

    Wait-ForDockerDaemon

    Write-Host "RabbitMQ is not reachable at 127.0.0.1:5672. Starting docker compose service rabbitmq ..."
    Push-Location $repoRoot
    try {
        $composeOutput = (& docker compose up -d rabbitmq 2>&1) -join [Environment]::NewLine
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start RabbitMQ with docker compose up -d rabbitmq.`n$composeOutput"
        }

        if (-not [string]::IsNullOrWhiteSpace($composeOutput)) {
            Write-Host $composeOutput.Trim()
        }
    }
    finally {
        Pop-Location
    }

    Wait-ForTcpEndpoint -HostName "127.0.0.1" -Port 5672 -TimeoutSeconds 90
}

function Assert-ProcessRunning {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$StdOutPath,
        [string]$StdErrPath
    )

    if ($Process.HasExited) {
        $stdout = Get-Content -LiteralPath $StdOutPath -Raw -ErrorAction SilentlyContinue
        $stderr = Get-Content -LiteralPath $StdErrPath -Raw -ErrorAction SilentlyContinue
        throw "$Name exited unexpectedly. ExitCode=$($Process.ExitCode)`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
    }
}

function Wait-ForHttpEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [System.Diagnostics.Process]$Process,
        [string]$StdOutPath,
        [string]$StdErrPath,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($null -ne $Process -and $Process.HasExited) {
            $stdout = Get-Content -LiteralPath $StdOutPath -Raw -ErrorAction SilentlyContinue
            $stderr = Get-Content -LiteralPath $StdErrPath -Raw -ErrorAction SilentlyContinue
            throw "Process exited before $Url was available. ExitCode=$($Process.ExitCode)`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
        }

        try {
            & curl.exe --silent --fail --insecure --max-time 5 $Url | Out-Null
            if ($LASTEXITCODE -eq 0) {
                return
            }
        }
        catch {
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Url"
}

function Test-TcpPortInUse {
    param([Parameter(Mandatory = $true)][int]$Port)

    return $null -ne (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Get-AvailableTcpPort {
    param([Parameter(Mandatory = $true)][int]$PreferredPort)

    $port = $PreferredPort
    while (Test-TcpPortInUse -Port $port) {
        $port++
    }

    return $port
}

function Redact-NgrokSensitiveOutput {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return $Text
    }

    return $Text -replace "(?is)(Your authtoken:\s*)\S+", '$1<redacted>'
}

function Wait-ForNgrokPublicUrl {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$NgrokProcess,
        [Parameter(Mandatory = $true)][string]$NgrokStdOutPath,
        [Parameter(Mandatory = $true)][string]$NgrokStdErrPath,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($NgrokProcess.HasExited) {
            $stderr = Redact-NgrokSensitiveOutput (Get-Content -LiteralPath $NgrokStdErrPath -Raw -ErrorAction SilentlyContinue)
            $stdout = Redact-NgrokSensitiveOutput (Get-Content -LiteralPath $NgrokStdOutPath -Raw -ErrorAction SilentlyContinue)
            throw "ngrok exited before opening a tunnel. ExitCode=$($NgrokProcess.ExitCode)`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
        }

        try {
            $response = Invoke-RestMethod -Uri "http://127.0.0.1:4040/api/tunnels" -TimeoutSec 5
            $publicUrl = $response.tunnels |
            Where-Object { $_.proto -eq "https" } |
            Select-Object -ExpandProperty public_url -First 1

            if (-not [string]::IsNullOrWhiteSpace($publicUrl)) {
                return $publicUrl.TrimEnd("/")
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    $stderr = Redact-NgrokSensitiveOutput (Get-Content -LiteralPath $NgrokStdErrPath -Raw -ErrorAction SilentlyContinue)
    $stdout = Redact-NgrokSensitiveOutput (Get-Content -LiteralPath $NgrokStdOutPath -Raw -ErrorAction SilentlyContinue)
    throw "Timed out waiting for ngrok public URL on http://127.0.0.1:4040/api/tunnels`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
}

function Stop-TrackedProcesses {
    param([System.Collections.Generic.List[System.Diagnostics.Process]]$Processes)

    foreach ($process in $Processes) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

if (-not (Test-Command -Name "dotnet")) {
    throw "dotnet CLI was not found in PATH."
}

if (-not (Test-Command -Name "ngrok")) {
    throw "ngrok CLI was not found in PATH."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Ensure-LocalRabbitMq

$logDirectory = Join-Path $repoRoot ".codex-logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$ngrokStdOutPath = Join-Path $logDirectory "run-local-n8n-ngrok.stdout.log"
$ngrokStdErrPath = Join-Path $logDirectory "run-local-n8n-ngrok.stderr.log"
$webApiStdOutPath = Join-Path $logDirectory "run-local-n8n-webapi.stdout.log"
$webApiStdErrPath = Join-Path $logDirectory "run-local-n8n-webapi.stderr.log"
$backgroundStdOutPath = Join-Path $logDirectory "run-local-n8n-background.stdout.log"
$backgroundStdErrPath = Join-Path $logDirectory "run-local-n8n-background.stderr.log"
$selectedWebApiPort = if ($WebApiScheme -eq "https") {
    Get-AvailableTcpPort -PreferredPort $WebApiHttpsPort
}
else {
    Get-AvailableTcpPort -PreferredPort $WebApiHttpPort
}

if (($WebApiScheme -eq "https" -and $selectedWebApiPort -ne $WebApiHttpsPort) -or
    ($WebApiScheme -eq "http" -and $selectedWebApiPort -ne $WebApiHttpPort)) {
    $preferredWebApiPort = if ($WebApiScheme -eq "https") { $WebApiHttpsPort } else { $WebApiHttpPort }
    Write-Host "Port $preferredWebApiPort is already in use. Using $selectedWebApiPort instead."
}

$webApiUrl = if ($WebApiScheme -eq "https") {
    "https://localhost:$selectedWebApiPort"
}
else {
    "http://localhost:$selectedWebApiPort"
}

$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

try {
    $ngrokArgs = @("http", $webApiUrl)
    if (-not [string]::IsNullOrWhiteSpace($NgrokDomain)) {
        $ngrokArgs += @("--domain", $NgrokDomain)
    }

    Write-Host "Starting ngrok tunnel for $webApiUrl ..."
    $ngrokProcess = Start-Process `
        -FilePath "ngrok" `
        -ArgumentList $ngrokArgs `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $ngrokStdOutPath `
        -RedirectStandardError $ngrokStdErrPath `
        -PassThru
    $processes.Add($ngrokProcess)

    $publicUrl = Wait-ForNgrokPublicUrl `
        -NgrokProcess $ngrokProcess `
        -NgrokStdOutPath $ngrokStdOutPath `
        -NgrokStdErrPath $ngrokStdErrPath
    $env:Modules__TestGeneration__N8nIntegration__BeBaseUrl = $publicUrl
    Write-Host "ngrok public URL: $publicUrl"

    Write-Host "Starting ClassifiedAds.WebAPI on $webApiUrl ..."
    $previousAspNetCoreUrls = $env:ASPNETCORE_URLS
    $env:ASPNETCORE_URLS = $webApiUrl
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $webApiArgs = @(
        "run",
        "--project", "ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj",
        "--no-launch-profile"
    )

    if ($SkipRestore) {
        $webApiArgs += "--no-restore"
    }

    $webApiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $webApiArgs `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $webApiStdOutPath `
        -RedirectStandardError $webApiStdErrPath `
        -PassThru
    $processes.Add($webApiProcess)
    $env:ASPNETCORE_URLS = $previousAspNetCoreUrls

    Wait-ForHttpEndpoint `
        -Url "$webApiUrl/alive" `
        -Process $webApiProcess `
        -StdOutPath $webApiStdOutPath `
        -StdErrPath $webApiStdErrPath

    Write-Host "Starting ClassifiedAds.Background with callback base URL $publicUrl ..."
    $env:DOTNET_ENVIRONMENT = "Development"
    $backgroundArgs = @(
        "run",
        "--project", "ClassifiedAds.Background/ClassifiedAds.Background.csproj"
    )

    if ($SkipRestore) {
        $backgroundArgs += "--no-restore"
    }

    $backgroundProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $backgroundArgs `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backgroundStdOutPath `
        -RedirectStandardError $backgroundStdErrPath `
        -PassThru
    $processes.Add($backgroundProcess)
    Start-Sleep -Seconds 5
    Assert-ProcessRunning `
        -Process $backgroundProcess `
        -Name "ClassifiedAds.Background" `
        -StdOutPath $backgroundStdOutPath `
        -StdErrPath $backgroundStdErrPath

    Write-Host ""
    Write-Host "Local n8n callback stack is running."
    Write-Host "WebAPI local:      $webApiUrl"
    Write-Host "Public callback:   $publicUrl"
    Write-Host "Callback base env: Modules__TestGeneration__N8nIntegration__BeBaseUrl=$publicUrl"
    Write-Host ""
    Write-Host "Use Ctrl+C to stop WebAPI, Background, and ngrok."

    while ($true) {
        Start-Sleep -Seconds 5
        Wait-ForHttpEndpoint `
            -Url "$webApiUrl/alive" `
            -Process $webApiProcess `
            -StdOutPath $webApiStdOutPath `
            -StdErrPath $webApiStdErrPath `
            -TimeoutSeconds 10
        Assert-ProcessRunning `
            -Process $backgroundProcess `
            -Name "ClassifiedAds.Background" `
            -StdOutPath $backgroundStdOutPath `
            -StdErrPath $backgroundStdErrPath
        [void](Wait-ForNgrokPublicUrl `
                -NgrokProcess $ngrokProcess `
                -NgrokStdOutPath $ngrokStdOutPath `
                -NgrokStdErrPath $ngrokStdErrPath `
                -TimeoutSeconds 10)
    }
}
finally {
    Stop-TrackedProcesses -Processes $processes
}
