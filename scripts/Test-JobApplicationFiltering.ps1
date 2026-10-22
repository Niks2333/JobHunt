param([string]$BackendUrl = 'http://localhost:5077', [string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)
. "$PSScriptRoot/Set-TestAuthentication.ps1" -AccessToken $AccessToken

$ErrorActionPreference = 'Stop'
$testMarker = 'JobHunt filter ' + [Guid]::NewGuid().ToString('N')
$identifiers = [System.Collections.Generic.List[int]]::new()
$searchQuery = [Uri]::EscapeDataString($testMarker)
$sourceA = "$testMarker source A"
$sourceB = "$testMarker source B"

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

try {
    $baselineTotal = (Invoke-RestMethod "$BackendUrl/api/applications").totalApplications
    for ($applicationNumber = 1; $applicationNumber -le 13; $applicationNumber++) {
        $isDeveloper = $applicationNumber % 2 -eq 1
        $request = @{
            companyName = "$testMarker $applicationNumber"
            roleTitle = if ($isDeveloper) { "$testMarker Developer" } else { "$testMarker Engineer" }
            applicationDate = if ($applicationNumber -le 10) { '2001-01-01' } else { '2000-01-01' }
            source = if ($isDeveloper) { $sourceA } else { $sourceB }
            notes = 'Temporary pagination verification'
        }
        $created = Invoke-RestMethod "$BackendUrl/api/applications" -Method Post -ContentType application/json -Body ($request | ConvertTo-Json)
        $identifiers.Add([int]$created.jobApplicationId)
        if ($applicationNumber -in 2, 5) {
            Invoke-RestMethod "$BackendUrl/api/applications/$($created.jobApplicationId)/status-changes" -Method Post -ContentType application/json -Body '{"status":"Interview","occurredOn":"2002-01-01"}' | Out-Null
        }
    }

    $firstPage = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery"
    $secondPage = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&pageNumber=2"
    Assert-Condition ($firstPage.items.Count -eq 10 -and $secondPage.items.Count -eq 3) 'Default pages must contain 10 and 3 records.'
    Assert-Condition ($firstPage.matchingApplications -eq 13 -and $firstPage.totalPages -eq 2 -and
        $firstPage.totalApplications -eq $baselineTotal + 13) 'Overall and matching counts must be distinct and accurate.'
    $combinedItems = @($firstPage.items) + @($secondPage.items)
    $expectedOrder = @($combinedItems | Sort-Object @{Expression='applicationDate';Descending=$true}, @{Expression='jobApplicationId';Descending=$true})
    Assert-Condition (($combinedItems.jobApplicationId -join ',') -eq ($expectedOrder.jobApplicationId -join ',') -and
        @($combinedItems.jobApplicationId | Select-Object -Unique).Count -eq 13) 'Paging must preserve newest-first ordering without duplicates.'

    $sourceQuery = [Uri]::EscapeDataString($sourceA)
    foreach ($sortBy in @('newest', 'oldest', 'company')) {
        $sortedFirst = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&sortBy=$sortBy"
        $sortedSecond = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&sortBy=$sortBy&pageNumber=2"
        $sortedItems = @($sortedFirst.items) + @($sortedSecond.items)
        $expectedSorted = switch ($sortBy) {
            'oldest' { @($combinedItems | Sort-Object applicationDate, jobApplicationId) }
            'company' { @($combinedItems | Sort-Object companyName, @{Expression='applicationDate';Descending=$true}, @{Expression='jobApplicationId';Descending=$true}) }
            default { $expectedOrder }
        }
        Assert-Condition (($sortedItems.jobApplicationId -join ',') -eq ($expectedSorted.jobApplicationId -join ',')) "Sort $sortBy must order all matches before paging, including ties."
        $filteredSorted = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&source=$sourceQuery&sortBy=$sortBy&pageSize=100"
        $expectedFiltered = @($expectedSorted | Where-Object source -eq $sourceA)
        Assert-Condition (($filteredSorted.items.jobApplicationId -join ',') -eq ($expectedFiltered.jobApplicationId -join ',')) "Sort $sortBy must work with combined search and source filters."
    }
    $combinedFilter = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&status=Interview&source=$sourceQuery"
    Assert-Condition ($combinedFilter.matchingApplications -eq 1 -and $combinedFilter.items[0].source -eq $sourceA -and
        $combinedFilter.items[0].status -eq 'Interview') 'Search, status, and source must combine with AND.'
    Assert-Condition ($combinedFilter.availableSources -contains $sourceA -and $combinedFilter.availableSources -contains $sourceB) 'Source options must remain available across filters.'
    $roleQuery = [Uri]::EscapeDataString("$testMarker Developer")
    $roleResults = Invoke-RestMethod "$BackendUrl/api/applications?search=$roleQuery"
    Assert-Condition ($roleResults.matchingApplications -eq 7) 'Search must find role titles, not just company names.'
    $trimmedQuery = [Uri]::EscapeDataString("  $testMarker  ")
    Assert-Condition ((Invoke-RestMethod "$BackendUrl/api/applications?search=$trimmedQuery").matchingApplications -eq 13) 'Search must trim surrounding whitespace.'
    $emptyQuery = [Uri]::EscapeDataString("$testMarker missing")
    $emptyResults = Invoke-RestMethod "$BackendUrl/api/applications?search=$emptyQuery"
    Assert-Condition ($emptyResults.items.Count -eq 0 -and $emptyResults.matchingApplications -eq 0 -and
        $emptyResults.totalPages -eq 0 -and $emptyResults.totalApplications -eq $baselineTotal + 13) 'No matches must preserve the overall count.'
    $beyondLastPage = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&pageNumber=3"
    Assert-Condition ($beyondLastPage.items.Count -eq 0 -and $beyondLastPage.totalPages -eq 2) 'Out-of-range pages must return an empty page with accurate metadata.'
    $smallPage = Invoke-RestMethod "$BackendUrl/api/applications?search=$searchQuery&pageSize=5"
    Assert-Condition ($smallPage.items.Count -eq 5 -and $smallPage.totalPages -eq 3) 'Custom page size must be respected.'

    foreach ($invalidQuery in @('pageNumber=0', 'pageNumber=-1', 'pageNumber=1000001', 'pageSize=0', 'pageSize=101', 'pageNumber=abc', 'status=Unknown', 'status=2', 'sortBy=unknown', 'sortBy=1', 'sortBy=')) {
        $invalidResponse = Invoke-WebRequest "$BackendUrl/api/applications?$invalidQuery" -SkipHttpErrorCheck
        Assert-Condition ($invalidResponse.StatusCode -eq 400) 'Invalid query options must return HTTP 400.'
    }
    Write-Output 'PASS: paging boundaries, stable ordering, combined filters, role/company search, whitespace, counts, source options, no matches, page sizes, and invalid options.'
}
finally {
    foreach ($identifier in $identifiers) {
        & sqlcmd -S localhost -d JobHunt -E -C -b -Q "SET NOCOUNT ON; DELETE FROM JobApplications WHERE JobApplicationId = $identifier AND CompanyName LIKE N'$testMarker%';"
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove filtering verification record $identifier." }
    }
}
