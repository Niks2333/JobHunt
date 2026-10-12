param([Parameter(Mandatory)][string]$Email)

$ErrorActionPreference = 'Stop'
$escapedEmail = $Email.Trim().Replace("'", "''")
$query = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @UserId nvarchar(450);
SELECT @UserId = Id FROM dbo.AspNetUsers WHERE NormalizedEmail = UPPER(N'$escapedEmail');
IF @UserId IS NULL THROW 50001, 'Register the target account before assigning applications.', 1;
UPDATE dbo.JobApplications SET UserId = @UserId WHERE UserId IS NULL;
SELECT @@ROWCOUNT AS AssignedApplications;
COMMIT;
"@
& sqlcmd -S localhost -d JobHunt -E -C -I -b -Q $query
if ($LASTEXITCODE -ne 0) { throw 'Legacy application assignment failed.' }
