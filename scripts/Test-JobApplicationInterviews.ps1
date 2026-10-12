param([string]$BackendUrl = 'http://localhost:5077', [string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)
. "$PSScriptRoot/Set-TestAuthentication.ps1" -AccessToken $AccessToken

$ErrorActionPreference = 'Stop'
$testMarker = 'JobHunt interviews ' + [Guid]::NewGuid().ToString('N')
$applicationIdentifiers = [System.Collections.Generic.List[int]]::new()
function Invoke-JsonResponse([string]$Uri, [string]$Method = 'Get', [string]$ContentType = 'application/json', [string]$Body) {
    $parameters = @{Uri=$Uri;Method=$Method;ContentType=$ContentType}
    if ($PSBoundParameters.ContainsKey('Body')) { $parameters.Body = $Body }
    (Invoke-WebRequest @parameters).Content | ConvertFrom-Json -DateKind String
}
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Send-Interview([int]$ApplicationIdentifier, [hashtable]$Request) {
    Invoke-JsonResponse "$BackendUrl/api/applications/$ApplicationIdentifier/interviews" -Method Post -ContentType application/json -Body ($Request | ConvertTo-Json)
}
try {
    foreach ($number in 1..2) {
        $application = Invoke-JsonResponse "$BackendUrl/api/applications" -Method Post -ContentType application/json -Body (@{companyName="$testMarker $number";roleTitle='Interview verification';applicationDate='2001-01-01'} | ConvertTo-Json)
        $applicationIdentifiers.Add([int]$application.jobApplicationId)
    }
    $applicationIdentifier = $applicationIdentifiers[0]
    $address = "$BackendUrl/api/applications/$applicationIdentifier/interviews"
    $futureTime = [DateTimeOffset]::UtcNow.AddDays(2).ToString('o')
    $request = @{ roundName='  Technical round 1  '; scheduledAt=$futureTime; mode='Online'; meetingUrl='https://example.com/interview'; location='  Online  '; preparationNotes='  Review LINQ  '; feedback='  Good discussion  '; isCancelled=$false }
    $created = Send-Interview $applicationIdentifier $request
    Assert-Condition ($created.roundName -eq 'Technical round 1' -and $created.preparationNotes -eq 'Review LINQ' -and $created.feedback -eq 'Good discussion') 'Inputs must be trimmed and preserved.'
    Assert-Condition ($created.scheduledAtUtc.EndsWith('Z') -and $created.createdAtUtc.EndsWith('Z')) 'Response timestamps must explicitly be UTC.'
    $past = Send-Interview $applicationIdentifier @{roundName='Past phone round';scheduledAt='2001-01-01T00:10:00+05:30';mode='Phone'}
    Assert-Condition ($past.scheduledAtUtc -eq '2000-12-31T18:40:00Z') 'IST midnight must convert to the previous UTC day without failing application-date validation.'
    $cancelled = Send-Interview $applicationIdentifier @{roundName='Cancelled round';scheduledAt=$futureTime;mode='InPerson';isCancelled=$true;location='Office'}
    $rounds = Invoke-JsonResponse $address
    Assert-Condition ($rounds.Count -eq 3 -and $rounds[0].jobApplicationInterviewId -eq $past.jobApplicationInterviewId) 'Multiple rounds must be listed in chronological order.'

    $request.roundName = 'Technical round rescheduled'
    $request.scheduledAt = [DateTimeOffset]::UtcNow.AddDays(3).ToString('o')
    $request.feedback = 'Updated feedback'
    $request.meetingUrl = ''
    $request.location = ''
    $request.preparationNotes = ''
    $updated = Invoke-JsonResponse "$address/$($created.jobApplicationInterviewId)" -Method Put -ContentType application/json -Body ($request | ConvertTo-Json)
    Assert-Condition ($updated.createdAtUtc -eq $created.createdAtUtc -and $updated.updatedAtUtc -ge $created.updatedAtUtc -and $updated.feedback -eq 'Updated feedback' -and $null -eq $updated.meetingUrl -and $null -eq $updated.preparationNotes) 'Editing must preserve creation time, reschedule, save feedback, and clear optional values.'

    $invalidRequests = @(
        @{roundName=' ';scheduledAt=$futureTime;mode='Online'},
        @{roundName='Round';mode='Phone'},
        @{roundName='Round';scheduledAt='2000-12-31T23:00:00+05:30';mode='Online'},
        @{roundName='Round';scheduledAt=$futureTime;mode='0'},
        @{roundName='Round';scheduledAt=$futureTime;mode='Unknown'},
        @{roundName='Round';scheduledAt=$futureTime;mode='Online';meetingUrl='javascript:alert(1)'},
        @{roundName=('A' * 151);scheduledAt=$futureTime;mode='Online'},
        @{roundName='Round';scheduledAt=$futureTime;mode='Online';feedback=('A' * 5001)}
    )
    foreach ($invalidRequest in $invalidRequests) {
        $response = Invoke-WebRequest "$address/$($created.jobApplicationInterviewId)" -Method Put -ContentType application/json -Body ($invalidRequest | ConvertTo-Json) -SkipHttpErrorCheck
        Assert-Condition ($response.StatusCode -eq 400) 'Invalid interviews must return HTTP 400.'
    }
    $preserved = (Invoke-JsonResponse $address | Where-Object jobApplicationInterviewId -eq $created.jobApplicationInterviewId)
    Assert-Condition ($preserved.roundName -eq $updated.roundName -and $preserved.updatedAtUtc -eq $updated.updatedAtUtc) 'Rejected edits must leave stored data unchanged.'
    $wrongOwner = Invoke-WebRequest "$BackendUrl/api/applications/$($applicationIdentifiers[1])/interviews/$($created.jobApplicationInterviewId)" -Method Put -ContentType application/json -Body ($request | ConvertTo-Json) -SkipHttpErrorCheck
    Assert-Condition ($wrongOwner.StatusCode -eq 404) 'A round cannot be edited under another application.'
    $missing = Invoke-WebRequest "$BackendUrl/api/applications/2147483647/interviews" -SkipHttpErrorCheck
    Assert-Condition ($missing.StatusCode -eq 404) 'Missing applications must return HTTP 404.'
    $applicationChange = Invoke-WebRequest "$BackendUrl/api/applications/$applicationIdentifier" -Method Put -ContentType application/json -Body (@{companyName="$testMarker 1";roleTitle='Interview verification';applicationDate='2001-01-02'} | ConvertTo-Json) -SkipHttpErrorCheck
    Assert-Condition ($applicationChange.StatusCode -eq 400) 'Application date cannot move beyond a saved interview.'
    $details = Invoke-JsonResponse "$BackendUrl/api/applications/$applicationIdentifier"
    Assert-Condition ($details.application.status -eq 'Applied' -and $details.statusHistory.Count -eq 1) 'Scheduling and editing rounds must not change application status/history.'

    # More than 10 upcoming rows exercises SQL pagination and total metadata.
    foreach ($number in 1..11) {
        Send-Interview $applicationIdentifier @{roundName="Upcoming round $number";scheduledAt=[DateTimeOffset]::UtcNow.AddDays(4 + $number).ToString('o');mode='Phone'} | Out-Null
    }
    $upcoming = Invoke-JsonResponse "$BackendUrl/api/interviews/upcoming"
    Assert-Condition ($upcoming.items.Count -eq 10 -and $upcoming.totalUpcomingInterviews -ge 12) 'Dashboard must return the next 10 with an accurate total.'
    $upcomingIdentifiers = $upcoming.items.interview.jobApplicationInterviewId
    Assert-Condition ($upcomingIdentifiers -notcontains $past.jobApplicationInterviewId -and $upcomingIdentifiers -notcontains $cancelled.jobApplicationInterviewId) 'Past and cancelled rounds must be excluded.'
    $ordered = @($upcoming.items | Sort-Object @{Expression={$_.interview.scheduledAtUtc}}, @{Expression={$_.interview.jobApplicationInterviewId}})
    Assert-Condition (($upcomingIdentifiers -join ',') -eq ($ordered.interview.jobApplicationInterviewId -join ',')) 'Upcoming rounds must be ordered earliest first.'
    $sqlCount = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM JobApplicationInterviews WHERE JobApplicationId = $applicationIdentifier;"
    if ($LASTEXITCODE -ne 0) { throw 'SQL verification failed.' }
    Assert-Condition ([int](($sqlCount | Where-Object { $_ -match '^\d+$' }) | Select-Object -First 1) -eq 14) 'All rounds must be persisted in SQL Server.'
    Write-Output 'PASS: create/list/edit rounds, UTC/IST boundary, notes/feedback, cancellation, upcoming limits/order, validation, ownership, chronology, preserved status, and SQL persistence.'
}
finally {
    foreach ($identifier in $applicationIdentifiers) {
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET NOCOUNT ON; DELETE FROM JobApplications WHERE JobApplicationId = $identifier AND CompanyName LIKE N'$testMarker%';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove interview verification application $identifier." }
    }
}
