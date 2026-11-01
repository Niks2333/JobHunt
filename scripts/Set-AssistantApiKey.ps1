# Run locally from PowerShell. Input is hidden and the key is passed through stdin,
# so it is not placed in shell history, command arguments, or repository files.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../backend/JobHunt.Api/JobHunt.Api.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$secretId = [string]($projectXml.Project.PropertyGroup | Where-Object { $_.UserSecretsId } | Select-Object -First 1).UserSecretsId
if ([string]::IsNullOrWhiteSpace($secretId)) { throw 'The API project has no UserSecretsId.' }
$secretFile = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json"
$secureKey = Read-Host 'Paste your Groq API key (input is hidden)' -AsSecureString
try {
    $plainKey = [System.Net.NetworkCredential]::new('', $secureKey).Password.Trim()
    if ([string]::IsNullOrWhiteSpace($plainKey)) { throw 'No key entered.' }
    @{ 'Assistant:GroqApiKey' = $plainKey } | ConvertTo-Json -Compress | dotnet user-secrets set --id $secretId
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the backend API key.' }
    $savedSettings = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
    if ($savedSettings.'Assistant:GroqApiKey' -cne $plainKey) {
        throw 'The key could not be verified in the expected secret store. No key value will be displayed.'
    }
    Write-Host 'Key saved and verified. Restart the backend, then open Ask JobHunt.'
    Write-Host "Secret store: $secretFile"
} finally {
    $plainKey = $null
    $savedSettings = $null
    $secureKey.Dispose()
}
