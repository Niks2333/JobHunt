# Run this in the same Windows PowerShell account used to save the Groq key.
# Each Windows execution context can have its own .NET user-secret store.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../backend/JobHunt.Api/JobHunt.Api.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$secretId = [string]($projectXml.Project.PropertyGroup | Where-Object { $_.UserSecretsId } | Select-Object -First 1).UserSecretsId
if ([string]::IsNullOrWhiteSpace($secretId)) { throw 'The API project has no UserSecretsId.' }
$secretFile = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json"
$localSettings = if (Test-Path $secretFile) { Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json } else { $null }
$signingKey = $localSettings.'Jwt:SigningKey'
if (-not $signingKey -and $localSettings.Jwt) { $signingKey = $localSettings.Jwt.SigningKey }

if ([string]::IsNullOrWhiteSpace($signingKey) -or [System.Text.Encoding]::UTF8.GetByteCount($signingKey) -lt 32) {
    # Create a local development signing key once. Keep existing account/password data.
    $bytes = New-Object byte[] 48
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    $signingKey = [Convert]::ToBase64String($bytes)
    @{ 'Jwt:SigningKey' = $signingKey } | ConvertTo-Json -Compress | dotnet user-secrets set --id $secretId
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure the local login signing key.' }
    Write-Host 'Created the local development login signing key.'
}
$groqConfigured = -not [string]::IsNullOrWhiteSpace($localSettings.'Assistant:GroqApiKey') -or
    -not [string]::IsNullOrWhiteSpace($localSettings.Assistant.GroqApiKey) -or
    -not [string]::IsNullOrWhiteSpace($env:GROQ_API_KEY) -or
    -not [string]::IsNullOrWhiteSpace($env:Assistant__GroqApiKey)
Write-Host "Groq key available in this PowerShell context: $groqConfigured"
$signingKey = $null
$localSettings = $null
Write-Host 'Starting the API at http://localhost:5077. Leave this window running.'
dotnet run --project $project --launch-profile http
