param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$Email = 'assistant.demo@jobhunt.example',
    [string]$BackendUrl = 'http://localhost:5077'
)

# Fictional data for the local learning account. Uses the normal API and Identity
# password hashing. Never deletes records or resets an existing account's password.
$ErrorActionPreference = 'Stop'
if ([Uri]$BackendUrl -and ([Uri]$BackendUrl).Host -notin @('localhost', '127.0.0.1', '[::1]')) {
    throw 'This demo seed script is intended for a localhost backend.'
}
$BackendUrl = $BackendUrl.TrimEnd('/')
$authBody = @{ email = $Email; password = $Password } | ConvertTo-Json
try {
    Invoke-RestMethod "$BackendUrl/api/auth/register" -Method Post -ContentType application/json -Body $authBody | Out-Null
} catch {
    if ([int]$_.Exception.Response.StatusCode -ne 400) { throw }
    # If the email exists, login must still succeed with the supplied password.
}
$session = Invoke-RestMethod "$BackendUrl/api/auth/login" -Method Post -ContentType application/json -Headers @{ 'X-JobHunt-CSRF' = '1' } -Body $authBody -SessionVariable demoSession
$headers = @{ Authorization = "Bearer $($session.accessToken)" }

function Send-DemoRequest([string]$Path, $Body) {
    Invoke-RestMethod "$BackendUrl$Path" -Method Post -Headers $headers -ContentType application/json -Body ($Body | ConvertTo-Json -Depth 8)
}

$today = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(5.5)).Date
# Company | Role | Source | days ago | stage history. All companies are invented.
$rows = @(
    'Cedarbyte Labs|Junior .NET Developer|LinkedIn|1|Applied',
    'Orbitleaf Systems|Angular Developer|LinkedIn|1|Applied',
    'Maplegrid Tech|Full Stack Developer|Naukri|1|Applied',
    'Bluefern Software|Backend Developer|Company website|0|Applied',
    'Pebblewave Apps|Software Engineer|LinkedIn|4|Applied',
    'Coppernest Digital|API Developer|Referral|5|Applied',
    'Northkite Labs|.NET Developer|LinkedIn|6|Assessment',
    'Silverbrook Apps|Frontend Developer|Naukri|7|Assessment',
    'Meadowstack Tech|Full Stack Engineer|Company website|8|Assessment',
    'Ambercloud Systems|Backend Engineer|LinkedIn|9|Assessment,Interview',
    'Birchloop Software|Angular Engineer|Naukri|10|Interview',
    'Velvetbit Labs|Software Developer|Referral|11|Interview',
    'Coralbridge Tech|API Engineer|Company website|12|Assessment,Interview',
    'Willowpath Systems|Full Stack Developer|LinkedIn|15|Assessment,Interview,Offer',
    'Brightmoss Software|.NET Engineer|Referral|16|Interview,Offer',
    'Pinepixel Labs|Frontend Engineer|LinkedIn|17|Interview,Rejected',
    'Cloudwren Tech|Backend Developer|Naukri|18|Assessment,Interview,Rejected',
    'Quartzleaf Apps|Software Engineer|Company website|19|Interview,Rejected',
    'Redwoodnest Digital|Angular Developer|Naukri|20|Rejected',
    'Sunlitstack Labs|Full Stack Engineer|LinkedIn|21|Withdrawn'
)

$existing = Invoke-RestMethod "$BackendUrl/api/applications?search=%5BAssistant%20Demo%5D&pageSize=100" -Headers $headers
$applications = @{}
$createdCount = 0
foreach ($row in $rows) {
    $company, $role, $source, $daysAgo, $stageText = $row.Split('|')
    $companyName = "[Assistant Demo] $company"
    $application = $existing.items | Where-Object { $_.companyName -eq $companyName } | Select-Object -First 1
    if (-not $application) {
        $applicationDate = $today.AddDays(-[int]$daysAgo).ToString('yyyy-MM-dd')
        $application = Send-DemoRequest '/api/applications' @{
            companyName = $companyName
            roleTitle = $role
            source = $source
            location = 'Pune / Remote'
            applicationDate = $applicationDate
            jobUrl = 'https://example.com/fictional-job'
            notes = 'Fictional learning dataset for Ask JobHunt. Skills: ASP.NET Core, Angular, SQL, REST APIs. No real application was submitted.'
        }
        foreach ($stage in $stageText.Split(',')) {
            if ($stage -eq 'Applied') { continue }
            $application = Send-DemoRequest "/api/applications/$($application.jobApplicationId)/status-changes" @{
                status = $stage
                occurredOn = $today.AddDays(-[int]$daysAgo + 1).ToString('yyyy-MM-dd')
            }
        }
        $createdCount++
    }
    $applications[$company] = $application
}

# Four historical phone rounds and two upcoming rounds for future assistant lessons.
$rounds = @(
    @{ company = 'Willowpath Systems'; days = -12; mode = 'Phone'; feedback = 'Strong API design. Revise SQL indexes and query execution plans.' },
    @{ company = 'Brightmoss Software'; days = -13; mode = 'Phone'; feedback = 'Clear explanation of JWT authentication and dependency injection.' },
    @{ company = 'Pinepixel Labs'; days = -14; mode = 'Phone'; feedback = 'Revise Angular change detection and RxJS cancellation.' },
    @{ company = 'Cloudwren Tech'; days = -15; mode = 'Phone'; feedback = 'Practice SQL joins and transactions with concrete examples.' },
    @{ company = 'Ambercloud Systems'; days = 1; mode = 'Online'; feedback = $null },
    @{ company = 'Birchloop Software'; days = 2; mode = 'Phone'; feedback = $null }
)
foreach ($round in $rounds) {
    $applicationId = $applications[$round.company].jobApplicationId
    $path = "/api/applications/$applicationId/interviews"
    $existingRounds = @(Invoke-RestMethod "$BackendUrl$path" -Headers $headers)
    if ($existingRounds | Where-Object { $_.roundName -eq '[Assistant Demo] First conversation' }) { continue }
    Send-DemoRequest $path @{
        roundName = '[Assistant Demo] First conversation'
        scheduledAt = $today.AddDays($round.days).ToString('yyyy-MM-dd') + 'T14:00:00+05:30'
        mode = $round.mode
        preparationNotes = 'Review project architecture, Angular forms, REST APIs, and SQL joins.'
        feedback = $round.feedback
        isCancelled = $false
    } | Out-Null
}

$analytics = Invoke-RestMethod "$BackendUrl/api/analytics/applications" -Headers $headers
$yesterday = $today.AddDays(-1).ToString('yyyy-MM-dd')
$yesterdayAnalytics = Invoke-RestMethod "$BackendUrl/api/analytics/applications?startDate=$yesterday&endDate=$yesterday" -Headers $headers
Invoke-RestMethod "$BackendUrl/api/auth/logout" -Method Post -Headers @{ 'X-JobHunt-CSRF' = '1' } -WebSession $demoSession | Out-Null

[pscustomobject]@{
    Email = $Email
    CreatedApplications = $createdCount
    TotalApplications = $analytics.totalApplications
    InterviewConversionPercentage = $analytics.interviewConversionPercentage
    ApplicationsYesterday = $yesterdayAnalytics.totalApplications
    Statuses = $analytics.applicationsByStatus
    Sources = $analytics.applicationsBySource
} | ConvertTo-Json -Depth 8
