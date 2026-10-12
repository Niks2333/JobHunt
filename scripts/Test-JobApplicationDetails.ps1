param([string]$BackendUrl = 'http://localhost:5077', [string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)
. "$PSScriptRoot/Set-TestAuthentication.ps1" -AccessToken $AccessToken

$ErrorActionPreference = 'Stop'
$jobApplications = (Invoke-RestMethod "$BackendUrl/api/applications?search=%5BSample%5D&pageSize=100").items
$sampleApplications = @($jobApplications | Where-Object companyName -like '[[]Sample]*')
if ($sampleApplications.Count -eq 0) {
    throw 'Seed the sample applications before running the details verification.'
}

foreach ($sampleApplication in $sampleApplications) {
    $applicationDetails = Invoke-RestMethod "$BackendUrl/api/applications/$($sampleApplication.jobApplicationId)"
    if ($applicationDetails.application.jobApplicationId -ne $sampleApplication.jobApplicationId -or
        $applicationDetails.application.notes -ne $sampleApplication.notes) {
        throw 'Details must return the requested application and its saved notes.'
    }
    $historyEntries = @($applicationDetails.statusHistory)
    $chronologicalEntries = @($historyEntries | Sort-Object occurredOn, jobApplicationStatusHistoryId)
    if (($historyEntries.jobApplicationStatusHistoryId -join ',') -ne
        ($chronologicalEntries.jobApplicationStatusHistoryId -join ',')) {
        throw 'Status history must be in chronological order.'
    }
    if ($historyEntries[0].status -ne 'Applied' -or
        $historyEntries[0].occurredOn -ne $sampleApplication.applicationDate -or
        $historyEntries[-1].status -ne $sampleApplication.status) {
        throw 'Sample history must match its application date and current stage.'
    }
    $expectedHistoryCount = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM JobApplicationStatusHistory WHERE JobApplicationId = $($sampleApplication.jobApplicationId);"
    if ($LASTEXITCODE -ne 0 -or $historyEntries.Count -ne [int]($expectedHistoryCount -join '').Trim()) {
        throw 'Details must include every saved history entry for this application.'
    }
}

foreach ($unknownIdentifier in @('2147483647', '0', '-1', 'not-a-number')) {
    $missingResponse = Invoke-WebRequest "$BackendUrl/api/applications/$unknownIdentifier" -SkipHttpErrorCheck
    if ($missingResponse.StatusCode -ne 404) { throw 'Unknown or invalid identifiers must return HTTP 404.' }
}

Write-Output "PASS: details and complete chronological history for $($sampleApplications.Count) sample applications; missing and invalid identifiers."
