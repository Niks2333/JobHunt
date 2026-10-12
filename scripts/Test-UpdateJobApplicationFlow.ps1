param([string]$BackendUrl = 'http://localhost:5077', [string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)
. "$PSScriptRoot/Set-TestAuthentication.ps1" -AccessToken $AccessToken

$ErrorActionPreference = 'Stop'
$testCompanyName = 'JobHunt edit verification ' + [Guid]::NewGuid().ToString('N')
$applicationIdentifier = $null

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Send-UpdateRequest($Request, [int]$Identifier) {
    Invoke-WebRequest "$BackendUrl/api/applications/$Identifier" -Method Put `
        -ContentType 'application/json' -Body ($Request | ConvertTo-Json) -SkipHttpErrorCheck
}

try {
    $creationRequest = @{
        companyName = $testCompanyName; roleTitle = 'Original role'; applicationDate = '2000-01-01'
        location = 'Pune'; source = 'Verification'; jobUrl = 'https://example.com/job'; notes = 'Original notes'
    }
    $createdApplication = Invoke-RestMethod "$BackendUrl/api/applications" -Method Post `
        -ContentType 'application/json' -Body ($creationRequest | ConvertTo-Json)
    $applicationIdentifier = [int]$createdApplication.jobApplicationId

    # Add a later stage only to this disposable record to verify chronology and status preservation.
    & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET XACT_ABORT ON; BEGIN TRANSACTION; INSERT INTO JobApplicationStatusHistory (JobApplicationId, Status, OccurredOn, RecordedAtUtc) VALUES ($applicationIdentifier, N'Interview', '2001-01-01', SYSUTCDATETIME()); UPDATE JobApplications SET Status = N'Interview' WHERE JobApplicationId = $applicationIdentifier; COMMIT;"
    Assert-Condition ($LASTEXITCODE -eq 0) 'Could not prepare disposable status history.'
    $originalDetails = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"

    $updateRequest = @{
        companyName = "  $testCompanyName updated  "; roleTitle = '  Updated role  '
        applicationDate = '1999-01-01'; location = 'Remote'; source = 'Referral'
        jobUrl = 'https://example.com/updated'; notes = "Updated notes`nSecond line"
        status = 'Offer' # Not an accepted edit field: it must not alter the status.
    }
    $updateResponse = Send-UpdateRequest $updateRequest $applicationIdentifier
    Assert-Condition ($updateResponse.StatusCode -eq 200) 'Valid update must return HTTP 200.'
    $updatedDetails = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
    $updatedApplication = $updatedDetails.application
    Assert-Condition ($updatedApplication.companyName -eq "$testCompanyName updated" -and
        $updatedApplication.roleTitle -eq 'Updated role') 'Updated text must be saved and trimmed.'
    Assert-Condition ($updatedApplication.location -eq 'Remote' -and $updatedApplication.source -eq 'Referral' -and
        $updatedApplication.jobUrl -eq 'https://example.com/updated' -and $updatedApplication.notes -eq $updateRequest.notes) 'All editable fields must persist.'
    Assert-Condition ($updatedApplication.status -eq 'Interview') 'Editing must preserve current status.'
    Assert-Condition ($updatedApplication.createdAtUtc -eq $originalDetails.application.createdAtUtc) 'Editing must preserve creation time.'
    Assert-Condition ($updatedApplication.updatedAtUtc -gt $originalDetails.application.updatedAtUtc) 'Editing must refresh the update timestamp.'
    Assert-Condition ($updatedDetails.statusHistory.Count -eq 2 -and
        $updatedDetails.statusHistory[0].occurredOn -eq '1999-01-01' -and
        $updatedDetails.statusHistory[1].occurredOn -eq '2001-01-01') 'Only the initial Applied event date should be corrected.'
    Assert-Condition ($updatedDetails.statusHistory[0].jobApplicationStatusHistoryId -eq
        $originalDetails.statusHistory[0].jobApplicationStatusHistoryId -and
        $updatedDetails.statusHistory[0].recordedAtUtc -eq $originalDetails.statusHistory[0].recordedAtUtc) 'Date correction must preserve history identity and recording time.'

    $invalidCases = @(
        @{Field='applicationDate'; Value='2002-01-01'}, # After the recorded interview
        @{Field='applicationDate'; Value='2099-01-01'},
        @{Field='companyName'; Value='   '},
        @{Field='roleTitle'; Value=('X' * 151)},
        @{Field='jobUrl'; Value='javascript:alert(1)'}
    )
    foreach ($invalidCase in $invalidCases) {
        $invalidRequest = $updateRequest.Clone()
        $invalidRequest[$invalidCase.Field] = $invalidCase.Value
        $invalidResponse = Send-UpdateRequest $invalidRequest $applicationIdentifier
        Assert-Condition ($invalidResponse.StatusCode -eq 400) 'Invalid edit must return HTTP 400.'
    }
    $afterInvalidRequests = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
    Assert-Condition (($afterInvalidRequests | ConvertTo-Json -Depth 8 -Compress) -eq
        ($updatedDetails | ConvertTo-Json -Depth 8 -Compress)) 'Rejected updates must not modify application or history.'

    $clearingRequest = $updateRequest.Clone()
    $clearingRequest.location = '   '; $clearingRequest.source = $null
    $clearingRequest.jobUrl = ''; $clearingRequest.notes = ''
    Assert-Condition ((Send-UpdateRequest $clearingRequest $applicationIdentifier).StatusCode -eq 200) 'Clearing optional fields must succeed.'
    $clearedApplication = (Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier").application
    Assert-Condition ($null -eq $clearedApplication.location -and $null -eq $clearedApplication.source -and
        $null -eq $clearedApplication.jobUrl -and $null -eq $clearedApplication.notes) 'Empty optional fields must become null.'
    Assert-Condition ((Send-UpdateRequest $updateRequest 2147483647).StatusCode -eq 404) 'Editing a missing application must return HTTP 404.'
    Write-Output 'PASS: editable fields, trimming, timestamps, status preservation, Applied date correction, chronology validation, rejected edits, optional clearing, and missing-record response.'
}
finally {
    if ($null -ne $applicationIdentifier) {
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "DELETE FROM JobApplications WHERE JobApplicationId = $applicationIdentifier AND CompanyName LIKE N'$testCompanyName%';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove disposable edit verification record $applicationIdentifier." }
    }
}
