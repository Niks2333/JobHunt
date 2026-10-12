param([string]$AccessToken = $env:JOBHUNT_TEST_ACCESS_TOKEN)

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw 'Provide -AccessToken or set JOBHUNT_TEST_ACCESS_TOKEN to a login access token before running tracker checks.'
}
$testTokenPayload = $AccessToken.Split('.')[1].Replace('-', '+').Replace('_', '/')
$testTokenPayload = $testTokenPayload.PadRight($testTokenPayload.Length + ((4 - $testTokenPayload.Length % 4) % 4), '=')
$TestUserId = ([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($testTokenPayload)) | ConvertFrom-Json).sub
if ($TestUserId -notmatch '^[0-9a-fA-F-]{36}$') { throw 'Expected a valid Identity user ID in the test token.' }
$PSDefaultParameterValues['Invoke-RestMethod:Headers'] = @{ Authorization = "Bearer $AccessToken" }
$PSDefaultParameterValues['Invoke-WebRequest:Headers'] = @{ Authorization = "Bearer $AccessToken" }
