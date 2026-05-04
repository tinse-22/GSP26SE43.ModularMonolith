$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5099'
$renderUrl = 'https://test-llm-api-testing.onrender.com'
$specFile = Join-Path $PSScriptRoot 'swagger.json'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runTag = "gitnexus-flow-$timestamp"

# Ensure output directory exists
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'artifacts\test-results') | Out-Null

$summary = [ordered]@{
  baseUrl = $base
  targetUrl = $renderUrl
  runTag = $runTag
  startedAt = (Get-Date).ToString('o')
}

function Invoke-JsonApi {
  param(
    [string]$Method,
    [string]$Path,
    [object]$Body,
    [hashtable]$Headers,
    [int]$TimeoutSec = 300
  )

  $uri = if ($Path.StartsWith('http')) { $Path } else { "$base$Path" }
  $Headers = if ($null -eq $Headers) { @{} } else { $Headers }

  if ($null -ne $Body) {
    $json = $Body | ConvertTo-Json -Depth 40 -Compress
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -ContentType 'application/json' -Body $json -TimeoutSec $TimeoutSec
  }

  return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -TimeoutSec $TimeoutSec
}

$stage = 'login'
try {
  Write-Host "Starting stage: $stage at $base"
  $login = Invoke-JsonApi -Method 'Post' -Path '/api/auth/login' -Body @{ email = 'tinvtse@gmail.com'; password = 'Admin@123' }
  $token = $login.accessToken
  if ([string]::IsNullOrWhiteSpace($token)) { $token = $login.token }
  if ([string]::IsNullOrWhiteSpace($token)) { throw 'Login succeeded but access token is missing.' }
  $auth = @{ Authorization = "Bearer $token" }
  Write-Host "[OK] LOGIN user=$($login.user.email)"

  $stage = 'create-project'
  Write-Host "Starting stage: $stage"
  $projectBody = @{
    name = "Project $runTag"
    description = "Full flow run from gitnexus-flow.ps1 targeting Render"
    baseUrl = $renderUrl
  }
  $project = Invoke-JsonApi -Method 'Post' -Path '/api/projects' -Body $projectBody -Headers $auth
  $projectId = [string]$project.id
  Write-Host "[OK] PROJECT created id=$projectId"

  $stage = 'upload-specification'
  Write-Host "Starting stage: $stage"
  if (-not (Test-Path $specFile)) { throw "Spec file not found: $specFile" }
  
  $specBytes = [System.IO.File]::ReadAllBytes($specFile)
  $boundary = [System.Guid]::NewGuid().ToString()
  $LF = "`r`n"
  $bodyLines = @(
    "--$boundary",
    'Content-Disposition: form-data; name="uploadMethod"',
    '',
    '0',
    "--$boundary",
    'Content-Disposition: form-data; name="name"',
    '',
    "spec-$timestamp",
    "--$boundary",
    'Content-Disposition: form-data; name="sourceType"',
    '',
    '0',
    "--$boundary",
    'Content-Disposition: form-data; name="version"',
    '',
    '1.0.0',
    "--$boundary",
    'Content-Disposition: form-data; name="autoActivate"',
    '',
    'true'
  )
  $textPart = ($bodyLines -join $LF) + $LF
  $textBytes = [System.Text.Encoding]::UTF8.GetBytes($textPart)
  $fileHeader = "--$boundary$LF" +
    "Content-Disposition: form-data; name=`"file`"; filename=`"swagger.json`"$LF" +
    "Content-Type: application/json$LF$LF"
  $fileHeaderBytes = [System.Text.Encoding]::UTF8.GetBytes($fileHeader)
  $fileFooter = "$LF--$boundary--$LF"
  $fileFooterBytes = [System.Text.Encoding]::UTF8.GetBytes($fileFooter)
  $fullBody = New-Object System.Collections.Generic.List[byte]
  $fullBody.AddRange($textBytes)
  $fullBody.AddRange($fileHeaderBytes)
  $fullBody.AddRange($specBytes)
  $fullBody.AddRange($fileFooterBytes)
  $uploadHeaders = @{ Authorization = "Bearer $token"; 'Content-Type' = "multipart/form-data; boundary=$boundary" }
  $spec = Invoke-RestMethod -Method Post -Uri "$base/api/projects/$projectId/specifications/upload" -Headers $uploadHeaders -Body $fullBody.ToArray() -TimeoutSec 120
  $specId = [string]$spec.id
  Write-Host "[OK] SPEC uploaded id=$specId status=$($spec.parseStatus)"

  $stage = 'create-test-suite'
  Write-Host "Starting stage: $stage"
  $endpoints = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/specifications/$specId/endpoints" -Headers $auth
  $selectedEndpointIds = @($endpoints | Select-Object -First 5 | ForEach-Object { $_.id })
  
  $suiteBody = @{
    name = "Suite $runTag"
    apiSpecId = $specId
    generationType = 'LLMAssisted'
    selectedEndpointIds = $selectedEndpointIds
  }
  $suite = Invoke-JsonApi -Method 'Post' -Path "/api/projects/$projectId/test-suites" -Body $suiteBody -Headers $auth
  $suiteId = [string]$suite.id
  Write-Host "[OK] SUITE created id=$suiteId"

  $stage = 'propose-order'
  Write-Host "Starting stage: $stage"
  $proposal = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/order-proposals" -Body @{ specificationId = $specId; source = 'Ai' } -Headers $auth
  $proposalId = [string]$proposal.proposalId
  $rowVersion = [string]$proposal.rowVersion
  Write-Host "[OK] ORDER proposed id=$proposalId"

  $stage = 'approve-order'
  Write-Host "Starting stage: $stage"
  $approved = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/order-proposals/$proposalId/approve" -Body @{ rowVersion = $rowVersion } -Headers $auth
  Write-Host "[OK] ORDER approved"

  $stage = 'generate-llm-suggestions'
  Write-Host "Starting stage: $stage (this may take a while...)"
  $llmResult = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/llm-suggestions/generate" -Body @{ specificationId = $specId; forceRefresh = $true } -Headers $auth -TimeoutSec 1800
  Write-Host "[OK] LLM suggestions generated total=$($llmResult.totalSuggestions)"

  $stage = 'bulk-approve'
  Write-Host "Starting stage: $stage"
  $bulk = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/llm-suggestions/bulk-review" -Body @{ action = 'Approve' } -Headers $auth
  Write-Host "[OK] BULK approve processed=$($bulk.processedCount)"

  $stage = 'traceability'
  Write-Host "Starting stage: $stage"
  $trace = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/test-suites/$suiteId/traceability" -Headers $auth
  Write-Host "[OK] TRACEABILITY fetched"

  Write-Host "FULL FLOW COMPLETED SUCCESSFULLY"
  $summary.success = $true
}
catch {
  Write-Host "ERROR at stage $stage : $($_.Exception.Message)"
  $summary.success = $false
  $summary.error = $_.Exception.Message
  $summary.failedStage = $stage
}

$summary | ConvertTo-Json | Set-Content -Path (Join-Path $PSScriptRoot "artifacts\test-results\$runTag.json")
