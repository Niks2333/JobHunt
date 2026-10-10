param(
    [string]$BackendUrl = 'http://localhost:5077'
)

$ErrorActionPreference = 'Stop'
$testCompanyName = 'JobHunt verification ' + [Guid]::NewGuid().ToString('N')
$createdApplicationIdentifiers = [System.Collections.Generic.List[int]]::new()
$todayInIndia = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(5.5)).ToString('yyyy-MM-dd')

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Send-ApplicationRequest($Request) {
    Invoke-WebRequest -Uri "$BackendUrl/api/applications" -Method Post `
        -ContentType 'application/json' -Body ($Request | ConvertTo-Json) -SkipHttpErrorCheck
}

function Read-JsonResponse($Response) {
    $responseText = if ($Response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($Response.Content)
    } else { [string]$Response.Content }
    $responseText | ConvertFrom-Json
}

try {
    $request = @{
        companyName = $testCompanyName
        roleTitle = '  Backend Developer  '
        applicationDate = $todayInIndia
        location = 'Pune'
        source = 'Verification'
        jobUrl = 'https://example.com/job'
        notes = 'Temporary integration check'
    }
    $creationResponse = Send-ApplicationRequest $request
    Assert-Condition ($creationResponse.StatusCode -eq 201) 'Valid creation must return HTTP 201.'
    $savedApplication = Read-JsonResponse $creationResponse
    $createdApplicationIdentifiers.Add([int]$savedApplication.jobApplicationId)
    Assert-Condition ($savedApplication.status -eq 'Applied') 'Initial status must be Applied.'
    Assert-Condition ($savedApplication.roleTitle -eq 'Backend Developer') 'Input should be trimmed.'
    $createdApplicationUrl = [Uri]::new([Uri]$BackendUrl, [string]$creationResponse.Headers.Location[0])
    $retrievedApplication = Invoke-RestMethod $createdApplicationUrl
    Assert-Condition ($retrievedApplication.application.jobApplicationId -eq $savedApplication.jobApplicationId) 'Location must resolve to the created record.'
    Assert-Condition ($retrievedApplication.statusHistory.Count -eq 1 -and $retrievedApplication.statusHistory[0].status -eq 'Applied') 'Details must include the initial status history.'
    $retrievedResponseText = [string](Invoke-WebRequest $createdApplicationUrl).Content
    Assert-Condition ($retrievedResponseText -match '"createdAtUtc":"[^"]+Z"') 'Retrieved timestamps must preserve UTC in JSON.'

    $historyCount = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM JobApplicationStatusHistory WHERE JobApplicationId = $($savedApplication.jobApplicationId) AND Status = N'Applied' AND OccurredOn = '$todayInIndia';"
    Assert-Condition ($LASTEXITCODE -eq 0 -and [int]($historyCount -join '').Trim() -eq 1) 'Exactly one initial status entry must preserve the application date.'

    $olderRequest = $request.Clone()
    $olderRequest.applicationDate = '2020-01-01'
    $olderRequest.location = $null
    $olderRequest.source = $null
    $olderRequest.jobUrl = $null
    $olderRequest.notes = $null
    $olderResponse = Send-ApplicationRequest $olderRequest
    Assert-Condition ($olderResponse.StatusCode -eq 201) 'Optional fields may be absent.'
    $olderApplication = Read-JsonResponse $olderResponse
    $createdApplicationIdentifiers.Add([int]$olderApplication.jobApplicationId)

    $listedApplications = Invoke-RestMethod "$BackendUrl/api/applications"
    $testApplications = @($listedApplications | Where-Object companyName -eq $testCompanyName)
    Assert-Condition ($testApplications.Count -eq 2) 'Both applications must be readable from SQL Server.'
    Assert-Condition ($testApplications[0].jobApplicationId -eq $savedApplication.jobApplicationId) 'Application date must control newest-first ordering.'

    $invalidCases = @(
        @{ Field = 'companyName'; Value = '   '; Description = 'Whitespace company' },
        @{ Field = 'roleTitle'; Value = ''; Description = 'Missing role' },
        @{ Field = 'companyName'; Value = ('X' * 151); Description = 'Oversized company' },
        @{ Field = 'applicationDate'; Value = '2099-01-01'; Description = 'Future date' },
        @{ Field = 'applicationDate'; Value = $null; Description = 'Missing date' },
        @{ Field = 'jobUrl'; Value = 'javascript:alert(1)'; Description = 'Unsafe link scheme' },
        @{ Field = 'jobUrl'; Value = 'not a URL'; Description = 'Malformed link' }
    )
    foreach ($invalidCase in $invalidCases) {
        $invalidRequest = $request.Clone()
        $invalidRequest[$invalidCase.Field] = $invalidCase.Value
        $invalidResponse = Send-ApplicationRequest $invalidRequest
        Assert-Condition ($invalidResponse.StatusCode -eq 400) "$($invalidCase.Description) must return HTTP 400."
        $validationResponse = Read-JsonResponse $invalidResponse
        Assert-Condition ($null -ne $validationResponse.errors) 'Validation response must contain field errors.'
    }
    $afterInvalidRequests = Invoke-RestMethod "$BackendUrl/api/applications"
    Assert-Condition (@($afterInvalidRequests | Where-Object companyName -eq $testCompanyName).Count -eq 2) 'Invalid requests must not create records.'
    $notFoundResponse = Invoke-WebRequest "$BackendUrl/api/applications/2147483647" -SkipHttpErrorCheck
    Assert-Condition ($notFoundResponse.StatusCode -eq 404) 'Unknown identifiers must return HTTP 404.'
    Write-Output 'PASS: create/read, Location, trimming, initial history, optional fields, ordering, seven validation cases, and missing-record response.'
}
finally {
    foreach ($applicationIdentifier in $createdApplicationIdentifiers) {
        # Remove only the uniquely marked records created by this script; history cascades.
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET NOCOUNT ON; DELETE FROM JobApplications WHERE JobApplicationId = $applicationIdentifier AND CompanyName = N'$testCompanyName';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not clean up verification record $applicationIdentifier." }
    }
}
