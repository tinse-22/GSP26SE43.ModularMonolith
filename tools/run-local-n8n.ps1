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

function Wait-ForHttpEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            & curl.exe --silent --show-error --fail --insecure --max-time 5 $Url | Out-Null
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
            $stderr = Get-Content -LiteralPath $NgrokStdErrPath -Raw -ErrorAction SilentlyContinue
            $stdout = Get-Content -LiteralPath $NgrokStdOutPath -Raw -ErrorAction SilentlyContinue
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

    $stderr = Get-Content -LiteralPath $NgrokStdErrPath -Raw -ErrorAction SilentlyContinue
    $stdout = Get-Content -LiteralPath $NgrokStdOutPath -Raw -ErrorAction SilentlyContinue
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
$logDirectory = Join-Path $repoRoot ".codex-logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$ngrokStdOutPath = Join-Path $logDirectory "run-local-n8n-ngrok.stdout.log"
$ngrokStdErrPath = Join-Path $logDirectory "run-local-n8n-ngrok.stderr.log"
$webApiUrl = if ($WebApiScheme -eq "https") {
    "https://localhost:$WebApiHttpsPort"
}
else {
    "http://localhost:$WebApiHttpPort"
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
    $webApiArgs = @(
        "run",
        "--project", "ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj",
        "--launch-profile", "https"
    )

    if ($SkipRestore) {
        $webApiArgs += "--no-restore"
    }

    $webApiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $webApiArgs `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -PassThru
    $processes.Add($webApiProcess)

    Wait-ForHttpEndpoint -Url "$webApiUrl/health"

    Write-Host "Starting ClassifiedAds.Background with callback base URL $publicUrl ..."
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
        -PassThru
    $processes.Add($backgroundProcess)

    Write-Host ""
    Write-Host "Local n8n callback stack is running."
    Write-Host "WebAPI local:      $webApiUrl"
    Write-Host "Public callback:   $publicUrl"
    Write-Host "Callback base env: Modules__TestGeneration__N8nIntegration__BeBaseUrl=$publicUrl"
    Write-Host ""
    Write-Host "Use Ctrl+C to stop WebAPI, Background, and ngrok."

    while ($true) {
        Start-Sleep -Seconds 5
        Wait-ForHttpEndpoint -Url "$webApiUrl/health" -TimeoutSeconds 10
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
