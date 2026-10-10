-- Fictional records for local UI checks. Run against the JobHunt database.
-- Existing records are preserved. Re-running this script does not duplicate samples.
USE [JobHunt];
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @RecordedAtUtc datetime2 = SYSUTCDATETIME();
    DECLARE @SampleApplications TABLE (
        CompanyName nvarchar(150), RoleTitle nvarchar(150), Location nvarchar(150),
        Source nvarchar(100), JobUrl nvarchar(2048), ApplicationDate date,
        Status nvarchar(30), Notes nvarchar(max)
    );

    INSERT INTO @SampleApplications VALUES
        (N'[Sample] Cedar Systems', N'.NET Developer', N'Pune', N'LinkedIn',
         N'https://example.com/jobs/cedar-dotnet', '2026-10-09', N'Applied',
         N'JobHunt sample data v1. Fictional application awaiting a response.'),
        (N'[Sample] Harbor Technologies', N'Backend Developer', N'Bengaluru', N'Naukri',
         NULL, '2026-10-07', N'Assessment',
         N'JobHunt sample data v1. Fictional coding assessment invitation.'),
        (N'[Sample] Maple Cloud', N'Full Stack Developer', N'Remote', N'Referral',
         N'https://example.com/jobs/maple-full-stack', '2026-10-05', N'Interview',
         N'JobHunt sample data v1. Fictional technical interview stage.'),
        (N'[Sample] Summit Software', N'ASP.NET Core Developer', N'Hyderabad', N'Company site',
         N'https://example.com/jobs/summit-backend', '2026-09-25', N'Offer',
         N'JobHunt sample data v1. Fictional offer after assessment and interview.'),
        (N'[Sample] Willow Digital', N'Angular Developer', N'Mumbai', N'LinkedIn',
         NULL, '2026-10-01', N'Rejected',
         N'JobHunt sample data v1. Fictional rejection before interview.'),
        (N'[Sample] Juniper Labs', N'Software Engineer', NULL, NULL,
         NULL, '2026-09-29', N'Withdrawn',
         N'JobHunt sample data v1. Fictional withdrawn application; optional fields are empty.');

    DECLARE @InsertedApplications TABLE (
        JobApplicationId int, CompanyName nvarchar(150), ApplicationDate date
    );

    INSERT INTO JobApplications (
        CompanyName, RoleTitle, Location, Source, JobUrl, ApplicationDate,
        Status, Notes, CreatedAtUtc, UpdatedAtUtc
    )
    OUTPUT inserted.JobApplicationId, inserted.CompanyName, inserted.ApplicationDate
        INTO @InsertedApplications
    SELECT sampleApplication.CompanyName, sampleApplication.RoleTitle,
        sampleApplication.Location, sampleApplication.Source, sampleApplication.JobUrl,
        sampleApplication.ApplicationDate, sampleApplication.Status, sampleApplication.Notes,
        @RecordedAtUtc, @RecordedAtUtc
    FROM @SampleApplications AS sampleApplication
    WHERE NOT EXISTS (
        SELECT 1 FROM JobApplications AS existingApplication
        WHERE existingApplication.CompanyName = sampleApplication.CompanyName
          AND existingApplication.RoleTitle = sampleApplication.RoleTitle
          AND existingApplication.Notes LIKE N'JobHunt sample data v1.%'
    );

    INSERT INTO JobApplicationStatusHistory (JobApplicationId, Status, OccurredOn, RecordedAtUtc)
    SELECT JobApplicationId, N'Applied', ApplicationDate, @RecordedAtUtc
    FROM @InsertedApplications;

    INSERT INTO JobApplicationStatusHistory (JobApplicationId, Status, OccurredOn, RecordedAtUtc)
    SELECT insertedApplication.JobApplicationId, historyEntry.Status,
        historyEntry.OccurredOn, @RecordedAtUtc
    FROM @InsertedApplications AS insertedApplication
    INNER JOIN (VALUES
        (N'[Sample] Harbor Technologies', N'Assessment', CONVERT(date, '2026-10-08')),
        (N'[Sample] Maple Cloud', N'Assessment', CONVERT(date, '2026-10-06')),
        (N'[Sample] Maple Cloud', N'Interview', CONVERT(date, '2026-10-08')),
        (N'[Sample] Summit Software', N'Assessment', CONVERT(date, '2026-09-27')),
        (N'[Sample] Summit Software', N'Interview', CONVERT(date, '2026-10-01')),
        (N'[Sample] Summit Software', N'Offer', CONVERT(date, '2026-10-05')),
        (N'[Sample] Willow Digital', N'Rejected', CONVERT(date, '2026-10-06')),
        (N'[Sample] Juniper Labs', N'Withdrawn', CONVERT(date, '2026-10-02'))
    ) AS historyEntry(CompanyName, Status, OccurredOn)
        ON historyEntry.CompanyName = insertedApplication.CompanyName;

    COMMIT TRANSACTION;

    SELECT COUNT(*) AS AddedSampleApplications FROM @InsertedApplications;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
