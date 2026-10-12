param([string]$BackendUrl = 'http://localhost:5077')

$ErrorActionPreference = 'Stop'
$testMarker = 'registration-test-' + [Guid]::NewGuid().ToString('N')
$firstEmail = "$testMarker-a@example.com"
$secondEmail = "$testMarker-b@example.com"
$raceEmail = "$testMarker-race@example.com"
$testPassword = 'StudyPass!2026'

function Send-Registration($Body) {
    Invoke-WebRequest "$BackendUrl/api/auth/register" -Method Post -ContentType 'application/json' `
        -Body ($Body | ConvertTo-Json -Compress) -SkipHttpErrorCheck
}

function Get-TestSql([string]$Query) {
    $output = & sqlcmd -S localhost -d JobHunt -E -C -I -b -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "Registration database verification failed: $($output -join ' ')" }
    return ($output -join '').Trim()
}

function Assert-Validation($Body, [string]$Field) {
    $response = Send-Registration $Body
    if ($response.StatusCode -ne 400) { throw "Expected HTTP 400 for $Field validation." }
    $content = if ($response.Content -is [byte[]]) {
        [System.Text.Encoding]::UTF8.GetString($response.Content)
    } else { $response.Content }
    $problem = $content | ConvertFrom-Json
    if (-not $problem.errors.$Field) { throw "Expected a validation message for $Field." }
}

function Read-NetworkInt([byte[]]$Bytes, [int]$Offset) {
    return [int]([uint32]$Bytes[$Offset] * 16777216 + [uint32]$Bytes[$Offset + 1] * 65536 +
        [uint32]$Bytes[$Offset + 2] * 256 + [uint32]$Bytes[$Offset + 3])
}

function Assert-PasswordHash([string]$Hash) {
    if ([string]::IsNullOrWhiteSpace($Hash) -or $Hash -eq $testPassword) { throw 'A password hash must be stored.' }
    # Identity V3 stores its PBKDF2 algorithm, iteration count, salt and subkey in this format.
    $bytes = [Convert]::FromBase64String($Hash)
    if ($bytes[0] -ne 1) { throw 'Expected an Identity V3 password hash.' }
    $algorithm = switch (Read-NetworkInt $bytes 1) {
        0 { [System.Security.Cryptography.HashAlgorithmName]::SHA1 }
        1 { [System.Security.Cryptography.HashAlgorithmName]::SHA256 }
        2 { [System.Security.Cryptography.HashAlgorithmName]::SHA512 }
        default { throw 'Unexpected password hash algorithm.' }
    }
    $iterations = Read-NetworkInt $bytes 5
    $saltLength = Read-NetworkInt $bytes 9
    [byte[]]$salt = $bytes[13..(12 + $saltLength)]
    [byte[]]$expected = $bytes[(13 + $saltLength)..($bytes.Length - 1)]
    $actual = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        $testPassword, $salt, $iterations, $algorithm, $expected.Length)
    $wrong = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        'WrongPassword!2026', $salt, $iterations, $algorithm, $expected.Length)
    if ([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String($expected) -or
        [Convert]::ToBase64String($wrong) -eq [Convert]::ToBase64String($expected)) {
        throw 'The stored password hash must match only the correct password.'
    }
}

try {
    $first = Send-Registration @{ email = $firstEmail; password = $testPassword }
    if ($first.StatusCode -ne 201) { throw 'Valid registration must return HTTP 201.' }
    $user = $first.Content | ConvertFrom-Json
    if (-not $user.id -or $user.email -ne $firstEmail -or $user.emailConfirmed -ne $false) {
        throw 'Registration must return the saved account without claiming email verification.'
    }
    if ((@($user.PSObject.Properties.Name | Sort-Object) -join ',') -ne 'email,emailConfirmed,id') {
        throw 'Registration must return only public account fields.'
    }
    if ($first.Headers.ContainsKey('Set-Cookie')) { throw 'Registration must not start a login session.' }

    $hash = Get-TestSql "SELECT PasswordHash FROM dbo.AspNetUsers WHERE Email = '$firstEmail';"
    Assert-PasswordHash $hash
    Assert-Validation @{ email = $firstEmail; password = $testPassword } 'Email'
    Assert-Validation @{ email = $firstEmail.ToUpperInvariant(); password = $testPassword } 'Email'
    Assert-Validation @{ email = 'not-an-email'; password = $testPassword } 'Email'
    Assert-Validation @{ password = $testPassword } 'Email'
    Assert-Validation @{ email = $secondEmail } 'Password'
    Assert-Validation @{ email = $secondEmail; password = 'Ab1!' } 'Password'
    Assert-Validation @{ email = $secondEmail; password = 'lowercase!2026' } 'Password'
    Assert-Validation @{ email = $secondEmail; password = 'UPPERCASE!2026' } 'Password'
    Assert-Validation @{ email = $secondEmail; password = 'StudyPassword!' } 'Password'
    Assert-Validation @{ email = $secondEmail; password = 'StudyPassword2026' } 'Password'
    Assert-Validation @{ email = $secondEmail; password = ('A' * 129) } 'Password'
    Assert-Validation @{ email = (('a' * 245) + '@example.com'); password = $testPassword } 'Email'
    $empty = Send-Registration @{}
    if ($empty.StatusCode -ne 400) { throw 'An empty registration request must be rejected.' }
    if ((Get-TestSql "SELECT COUNT(*) FROM dbo.AspNetUsers WHERE Email IN ('$firstEmail', '$secondEmail');") -ne '1') {
        throw 'Invalid and duplicate requests must not create accounts.'
    }

    $second = Send-Registration @{ email = $secondEmail; password = $testPassword }
    if ($second.StatusCode -ne 201) { throw 'A second distinct account must register.' }
    $secondHash = Get-TestSql "SELECT PasswordHash FROM dbo.AspNetUsers WHERE Email = '$secondEmail';"
    Assert-PasswordHash $secondHash
    if ($secondHash -eq $hash) { throw 'Identical passwords must receive different salted hashes.' }

    $raceStatuses = 1..2 | ForEach-Object -Parallel {
        $response = Invoke-WebRequest "$using:BackendUrl/api/auth/register" -Method Post `
            -ContentType 'application/json' -Body (@{ email = $using:raceEmail; password = $using:testPassword } |
                ConvertTo-Json -Compress) -SkipHttpErrorCheck
        [int]$response.StatusCode
    } -ThrottleLimit 2
    if (($raceStatuses | Sort-Object) -join ',' -ne '201,400') {
        throw 'Concurrent registrations must create exactly one account and reject the duplicate.'
    }
    if ((Get-TestSql "SELECT COUNT(*) FROM dbo.AspNetUsers WHERE Email = '$raceEmail';") -ne '1') {
        throw 'Concurrent registration must save exactly one account.'
    }
    Write-Output 'PASS: registration, safe response, validation, duplicate and concurrent emails, SQL persistence, correct-password verification, and distinct salted hashes.'
}
finally {
    # Remove only accounts using this run's GUID-generated email addresses.
    Get-TestSql "DELETE FROM dbo.AspNetUsers WHERE Email IN ('$firstEmail', '$secondEmail', '$raceEmail');" | Out-Null
}
