param([string]$BackendUrl = 'http://localhost:5077')

$ErrorActionPreference = 'Stop'
$testMarker = 'JobHunt analytics ' + [Guid]::NewGuid().ToString('N')
$identifiers = [System.Collections.Generic.List[int]]::new()

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

try {
    $baseline = Invoke-RestMethod "$BackendUrl/api/analytics/applications?startDate=1998-04-01&endDate=1998-04-30"
    for ($applicationNumber = 1; $applicationNumber -le 3; $applicationNumber++) {
        $request = @{
            companyName = "$testMarker $applicationNumber"
            roleTitle = 'Analytics verification'
            applicationDate = if ($applicationNumber -le 2) { '1998-04-01' } else { '1998-04-30' }
            source = if ($applicationNumber -eq 1) { $testMarker } else { $null }
        }
        $created = Invoke-RestMethod "$BackendUrl/api/applications" -Method Post -ContentType application/json -Body ($request | ConvertTo-Json)
        $identifiers.Add([int]$created.jobApplicationId)
        if ($applicationNumber -eq 1) {
            # Two interview entries, followed by rejection; still one reached-interview application.
            foreach ($status in @('Interview', 'Assessment', 'Interview', 'Rejected')) {
                Invoke-RestMethod "$BackendUrl/api/applications/$($created.jobApplicationId)/status-changes" -Method Post -ContentType application/json -Body (@{status=$status;occurredOn='1998-05-01'} | ConvertTo-Json) | Out-Null
            }
        }
        if ($applicationNumber -eq 2) {
            # An offer is not evidence of an interview without an interview history entry.
            Invoke-RestMethod "$BackendUrl/api/applications/$($created.jobApplicationId)/status-changes" -Method Post -ContentType application/json -Body '{"status":"Offer","occurredOn":"1998-05-01"}' | Out-Null
        }
    }
    $analytics = Invoke-RestMethod "$BackendUrl/api/analytics/applications?startDate=1998-04-01&endDate=1998-04-30"
    Assert-Condition ($analytics.totalApplications -eq $baseline.totalApplications + 3) 'Inclusive date range must contain both boundary dates.'
    Assert-Condition ($analytics.applicationsReachingInterview -eq $baseline.applicationsReachingInterview + 1) 'Interview history must count applications once even after rejection and outside the applied-date range.'
    Assert-Condition ($analytics.applicationsThisMonth -eq 0) 'Historical range must not contain current-month applications.'
    $expectedConversion = [Math]::Round(100.0 * $analytics.applicationsReachingInterview / $analytics.totalApplications, 1)
    Assert-Condition ($analytics.interviewConversionPercentage -eq $expectedConversion) 'Conversion must use the selected application cohort.'
    Assert-Condition (($analytics.applicationsByStatus | Measure-Object count -Sum).Sum -eq $analytics.totalApplications) 'Status breakdown must sum to the total.'
    Assert-Condition (($analytics.applicationsBySource | Measure-Object count -Sum).Sum -eq $analytics.totalApplications) 'Source breakdown must sum to the total.'
    Assert-Condition (($analytics.applicationsBySource | Where-Object label -eq $testMarker).count -eq 1) 'Named source must appear with its correct count.'
    Assert-Condition ($analytics.applicationsByStatus.Count -eq 6) 'All six statuses must appear, including zero counts.'
    $empty = Invoke-RestMethod "$BackendUrl/api/analytics/applications?startDate=9999-12-31&endDate=9999-12-31"
    Assert-Condition ($empty.totalApplications -eq 0 -and $empty.interviewConversionPercentage -eq 0 -and $empty.applicationsBySource.Count -eq 0) 'Empty cohorts must return zeros without dividing by zero.'
    $openRange = Invoke-RestMethod "$BackendUrl/api/analytics/applications?startDate=1998-04-01"
    Assert-Condition ($openRange.totalApplications -ge $analytics.totalApplications -and $null -eq $openRange.endDate) 'Single-ended ranges must be supported.'
    foreach ($invalidQuery in @('startDate=1998-05-01&endDate=1998-04-01', 'startDate=invalid', 'endDate=2026-02-30', 'startDate=0001-01-01')) {
        $response = Invoke-WebRequest "$BackendUrl/api/analytics/applications?$invalidQuery" -SkipHttpErrorCheck
        Assert-Condition ($response.StatusCode -eq 400) 'Invalid dates must return HTTP 400.'
    }

    # Independently recompute the all-time cards using SQL, rather than the LINQ implementation.
    $sqlCounts = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -s ',' -Q "SET NOCOUNT ON; SELECT COUNT(*), COALESCE(SUM(HasInterviewHistory), 0) FROM (SELECT CASE WHEN EXISTS (SELECT 1 FROM JobApplicationStatusHistory history WHERE history.JobApplicationId = application.JobApplicationId AND history.Status = 'Interview') THEN 1 ELSE 0 END AS HasInterviewHistory FROM JobApplications application) applications;"
    if ($LASTEXITCODE -ne 0) { throw 'SQL verification failed.' }
    $counts = (($sqlCounts | Where-Object { $_ -match '^\d+,\d+$' }) -split ',')
    $allTime = Invoke-RestMethod "$BackendUrl/api/analytics/applications"
    Assert-Condition ($allTime.totalApplications -eq [int]$counts[0] -and $allTime.applicationsReachingInterview -eq [int]$counts[1]) 'All-time cards must match independent SQL counts.'
    $monthStart = $allTime.currentMonthStartDate
    $monthCounts = & sqlcmd -S localhost -d JobHunt -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM JobApplications WHERE ApplicationDate >= '$monthStart' AND ApplicationDate < DATEADD(month, 1, CAST('$monthStart' AS date));"
    if ($LASTEXITCODE -ne 0) { throw 'Month SQL verification failed.' }
    Assert-Condition ($allTime.applicationsThisMonth -eq [int](($monthCounts | Where-Object { $_ -match '^\d+$' }) | Select-Object -First 1)) 'Monthly card must match SQL.'
    Write-Output 'PASS: date boundaries, repeated interviews, later rejection, offer without interview, counts, source/status breakdowns, empty/open ranges, validation, and independent SQL totals.'
}
finally {
    foreach ($identifier in $identifiers) {
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET NOCOUNT ON; DELETE FROM JobApplications WHERE JobApplicationId = $identifier AND CompanyName LIKE N'$testMarker%';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove analytics verification record $identifier." }
    }
}
