param([string]$BackendUrl = 'http://localhost:5077', [string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)
. "$PSScriptRoot/Set-TestAuthentication.ps1" -AccessToken $AccessToken

$ErrorActionPreference = 'Stop'
$testCompanyName = 'JobHunt status verification ' + [Guid]::NewGuid().ToString('N')
$applicationIdentifier = $null

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Send-StatusChange($Request, [int]$Identifier) {
    Invoke-WebRequest "$BackendUrl/api/applications/$Identifier/status-changes" -Method Post `
        -ContentType 'application/json' -Body ($Request | ConvertTo-Json) -SkipHttpErrorCheck
}

try {
    $creationRequest = @{ companyName=$testCompanyName; roleTitle='Status verification'; applicationDate='2000-01-01'; notes='Temporary status check' }
    $createdApplication = Invoke-RestMethod "$BackendUrl/api/applications" -Method Post `
        -ContentType 'application/json' -Body ($creationRequest | ConvertTo-Json)
    $applicationIdentifier = [int]$createdApplication.jobApplicationId
    $originalDetails = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
    $statusChanges = @(
        @{status='Assessment'; occurredOn='2000-01-02'},
        @{status='Interview'; occurredOn='2000-01-02'}, # Two real stages may occur on the same date.
        @{status='Offer'; occurredOn='2000-01-04'},
        @{status='Rejected'; occurredOn='2000-01-05'},
        @{status='Withdrawn'; occurredOn='2000-01-06'},
        @{status='Applied'; occurredOn='2000-01-07'} # Reopening preserves earlier history.
    )
    $expectedHistoryCount = 1
    foreach ($statusChange in $statusChanges) {
        $response = Send-StatusChange $statusChange $applicationIdentifier
        Assert-Condition ($response.StatusCode -eq 200) 'Valid status change must return HTTP 200.'
        $expectedHistoryCount++
        $details = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
        Assert-Condition ($details.application.status -eq $statusChange.status) 'Current status must match the change.'
        Assert-Condition ($details.statusHistory.Count -eq $expectedHistoryCount -and
            $details.statusHistory[-1].status -eq $statusChange.status -and
            $details.statusHistory[-1].occurredOn -eq $statusChange.occurredOn) 'Each successful change must append exactly one dated event.'
        Assert-Condition ($details.application.createdAtUtc -eq $originalDetails.application.createdAtUtc -and
            $details.application.applicationDate -eq '2000-01-01' -and
            $details.application.notes -eq 'Temporary status check') 'Status changes must preserve application details.'
        Assert-Condition ($details.application.updatedAtUtc -gt $originalDetails.application.updatedAtUtc) 'Status changes must update the modification timestamp.'
        Assert-Condition ($details.statusHistory[0].jobApplicationStatusHistoryId -eq
            $originalDetails.statusHistory[0].jobApplicationStatusHistoryId -and
            $details.statusHistory[0].occurredOn -eq '2000-01-01') 'Original history must remain intact.'
    }

    $detailsBeforeInvalidRequests = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
    $invalidRequests = @(
        @{status='Applied'; occurredOn='2000-01-07'}, # Duplicate current status
        @{status='Interview'; occurredOn='2000-01-06'}, # Earlier than latest event
        @{status='Interview'; occurredOn='1999-01-01'}, # Earlier than application
        @{status='Interview'; occurredOn='2099-01-01'}, # Future event
        @{status='Interview'}, # Missing date
        @{status='Unknown'; occurredOn='2000-01-07'},
        @{status='2'; occurredOn='2000-01-07'}, # Numeric enum strings must be rejected
        @{status='interview'; occurredOn='2000-01-07'},
        @{status=''; occurredOn='2000-01-07'}
    )
    foreach ($invalidRequest in $invalidRequests) {
        $response = Send-StatusChange $invalidRequest $applicationIdentifier
        Assert-Condition ($response.StatusCode -eq 400) 'Invalid status changes must return HTTP 400.'
    }
    $detailsAfterInvalidRequests = Invoke-RestMethod "$BackendUrl/api/applications/$applicationIdentifier"
    Assert-Condition (($detailsBeforeInvalidRequests | ConvertTo-Json -Depth 8 -Compress) -eq
        ($detailsAfterInvalidRequests | ConvertTo-Json -Depth 8 -Compress)) 'Rejected changes must not modify status, timestamps, or history.'
    $savedHistoryCount = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM JobApplicationStatusHistory WHERE JobApplicationId = $applicationIdentifier;"
    Assert-Condition ($LASTEXITCODE -eq 0 -and [int]($savedHistoryCount -join '').Trim() -eq 7) 'All seven history entries must persist in SQL Server.'
    Assert-Condition ((Send-StatusChange @{status='Interview'; occurredOn='2000-01-07'} 2147483647).StatusCode -eq 404) 'Changing a missing application must return HTTP 404.'
    Write-Output 'PASS: all stages, same-day changes, reopening, preserved details/history, timestamps, nine invalid requests, SQL persistence, and missing-record response.'
}
finally {
    if ($null -ne $applicationIdentifier) {
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET NOCOUNT ON; DELETE FROM JobApplications WHERE JobApplicationId = $applicationIdentifier AND CompanyName = N'$testCompanyName';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove status verification record $applicationIdentifier." }
    }
}
