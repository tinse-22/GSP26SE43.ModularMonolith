$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5099'
$renderUrl = 'https://test-llm-api-testing.onrender.com/'
$specFile = 'D:\GSP26SE43.ModularMonolith\swagger.json'
$srsFile = 'D:\GSP26SE43.ModularMonolith\test-srs.md'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runTag = "full-flow-$timestamp"

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

Write-Host "--- STARTING FULL FLOW ---"

# Login
Write-Host "[STEP 0] Login..."
$login = Invoke-JsonApi -Method 'Post' -Path '/api/auth/login' -Body @{ email = 'tinvtse@gmail.com'; password = 'Admin@123' }
$token = if ($login.accessToken) { $login.accessToken } else { $login.token }
$auth = @{ Authorization = "Bearer $token" }
Write-Host "[OK] Logged in as $($login.user.email)"

# STEP-0A: Create Project
Write-Host "[STEP-0A] Creating project..."
$project = Invoke-JsonApi -Method 'Post' -Path '/api/projects' -Body @{ name = "Project $runTag"; description = "Full flow test"; baseUrl = $renderUrl } -Headers $auth
$projectId = $project.id
Write-Host "[OK] Project created: $projectId"

# STEP-0B: Upload Specification
Write-Host "[STEP-0B] Uploading specification..."
$uploadForm = @{
  uploadMethod = '0'
  file = Get-Item $specFile
  name = "Spec $timestamp"
  sourceType = '0'
  version = '1.0.0'
  autoActivate = 'true'
}
$spec = Invoke-RestMethod -Method Post -Uri "$base/api/projects/$projectId/specifications/upload" -Headers $auth -Form $uploadForm
$apiSpecId = $spec.id
Write-Host "[OK] Specification uploaded: $apiSpecId"

# STEP-0C: Create Test Suite
Write-Host "[STEP-0C] Creating test suite..."
$endpoints = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/specifications/$apiSpecId/endpoints" -Headers $auth
$selectedEndpointIds = @($endpoints | Select-Object -First 5 | ForEach-Object { $_.id })

$suiteBody = @{
  name = "Suite $runTag"
  apiSpecId = $apiSpecId
  generationType = 'LLMAssisted'
  selectedEndpointIds = $selectedEndpointIds
}
$suite = Invoke-JsonApi -Method 'Post' -Path "/api/projects/$projectId/test-suites" -Body $suiteBody -Headers $auth
$suiteId = $suite.id
Write-Host "[OK] Test suite created: $suiteId"

# STEP-1: Upload SRS
Write-Host "[STEP-1] Uploading SRS document..."
$srsContent = Get-Content $srsFile -Raw
$srsRequest = @{
  title = "SRS $timestamp"
  testSuiteId = $suiteId
  sourceType = 0
  rawContent = $srsContent
}
$srsDoc = Invoke-JsonApi -Method 'Post' -Path "/api/projects/$projectId/srs-documents" -Body $srsRequest -Headers $auth
$srsDocumentId = $srsDoc.id
Write-Host "[OK] SRS document uploaded: $srsDocumentId"

# STEP-2: Analyze SRS
Write-Host "[STEP-2] Triggering SRS analysis..."
$analyzeResponse = Invoke-JsonApi -Method 'Post' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/analyze" -Headers $auth
$jobId = $analyzeResponse.jobId
Write-Host "[OK] Analysis triggered, jobId: $jobId"

# STEP-2-POLL
Write-Host "[STEP-2-POLL] Polling analysis job..."
$status = 0
while ($status -ne 3 -and $status -ne 4) {
  Start-Sleep -Seconds 5
  $job = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/analysis-jobs/$jobId" -Headers $auth
  $status = $job.status
  Write-Host "Job status: $status ($($job.statusDescription))"
  if ($status -eq 4) { throw "Analysis job failed: $($job.errorMessage)" }
}
Write-Host "[OK] Analysis completed."

# STEP-3: Get Requirements
Write-Host "[STEP-3] Fetching requirements..."
$requirements = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/requirements" -Headers $auth
Write-Host "[OK] Found $($requirements.Count) requirements."

foreach ($req in $requirements) {
  $reqId = $req.id
  Write-Host "Processing requirement: $($req.title) ($reqId)"

  # STEP-3A: Patch Requirement (isReviewed=true)
  Write-Host "  [STEP-3A] Reviewing requirement..."
  Invoke-JsonApi -Method 'Patch' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/requirements/$reqId" -Body @{ isReviewed = $true } -Headers $auth | Out-Null

  # STEP-3B: Get Clarifications
  Write-Host "  [STEP-3B] Checking clarifications..."
  $clarifications = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/requirements/$reqId/clarifications" -Headers $auth
  
  foreach ($clar in $clarifications) {
    if ($null -eq $clar.userAnswer) {
      # STEP-3C: Patch Clarification
      Write-Host "    [STEP-3C] Answering clarification: $($clar.question)"
      Invoke-JsonApi -Method 'Patch' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/requirements/$reqId/clarifications/$($clar.id)" -Body @{ userAnswer = "Confirmed." } -Headers $auth | Out-Null
    }
  }

  # STEP-3D: Refine Requirement
  Write-Host "  [STEP-3D] Refining requirement..."
  $refineResponse = Invoke-JsonApi -Method 'Post' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/requirements/$reqId/refine" -Headers $auth
  $refineJobId = $refineResponse.jobId
  
  # Poll refinement
  $refineStatus = 0
  while ($refineStatus -ne 3 -and $refineStatus -ne 4) {
    Start-Sleep -Seconds 2
    $refineJob = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/srs-documents/$srsDocumentId/analysis-jobs/$refineJobId" -Headers $auth
    $refineStatus = $refineJob.status
    if ($refineStatus -eq 4) { Write-Host "    Warning: Refinement job failed: $($refineJob.errorMessage)" ; break }
  }
  Write-Host "  [OK] Requirement refined."
}

# Propose and Approve Order (Prerequisite for Generation)
Write-Host "[STEP-3E] Proposing API order..."
$proposal = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/order-proposals" -Body @{ specificationId = $apiSpecId; source = 'Ai' } -Headers $auth
$proposalId = $proposal.proposalId
$rowVersion = $proposal.rowVersion
Write-Host "[OK] Order proposed: $proposalId"

Write-Host "[STEP-3F] Approving API order..."
Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/order-proposals/$proposalId/approve" -Body @{ rowVersion = $rowVersion } -Headers $auth | Out-Null
Write-Host "[OK] Order approved."

# STEP-4: Generate LLM Suggestions
Write-Host "[STEP-4] Generating LLM suggestions..."
$genResponse = Invoke-JsonApi -Method 'Post' -Path "/api/test-suites/$suiteId/llm-suggestions/generate" -Body @{ SpecificationId = $apiSpecId; forceRefresh = $true } -Headers $auth -TimeoutSec 1800
Write-Host "[OK] Generated $($genResponse.totalSuggestions) suggestions."

# STEP-5: Traceability
Write-Host "[STEP-5] Fetching traceability matrix..."
$trace = Invoke-JsonApi -Method 'Get' -Path "/api/projects/$projectId/test-suites/$suiteId/traceability" -Headers $auth
Write-Host "[OK] Traceability matrix fetched."

Write-Host "--- FULL FLOW COMPLETED ---"
