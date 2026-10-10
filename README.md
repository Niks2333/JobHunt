# JobHunt

A personal job application tracker, built step by step with Angular and ASP.NET Core.

## What works now

- My Applications screen with the total count and newest-first application list.
- Search company or role, filter by current status and source, and browse 10 applications
  per page. Apply Filters runs the search; Clear Filters returns to the unfiltered first page.
  Total applications and matching applications are shown separately.
- Click a company name to open Application Details: full saved information, notes,
  current stage, and chronological status history. Includes loading, retry, and not-found states.
- Add Application dialog: company, role, application date, location, source, job link, notes.
- Edit Application from the details page using the same prefilled form. Editing preserves
  status and creation time, refreshes UpdatedAtUtc, and corrects the initial Applied date
  when the application date changes. A date after an existing stage change is rejected.
- Change Status from the details page with a stage and occurrence date. Each successful
  change updates the current stage and appends history in a single transaction. Duplicate
  current stages, future dates, and dates before the latest event are rejected.
- Required-field, length, date, and HTTP/HTTPS link validation.
- SQL Server persistence with an initial Applied history entry saved in the same transaction.
- Dashboard analytics with application-date filters, interview conversion, and status/source breakdowns.
- Add/edit interview rounds with IST scheduling, meeting details, preparation notes, feedback,
  and cancellation. The dashboard lists the next 10 upcoming rounds.
- Loading, empty, success, and error states. Failed saves keep the form values.

Current scope is a local single-user tracker with applications, status history, analytics,
and interview rounds. Login and AI are later milestones. Six fictional applications were added
on request, labeled `[Sample]`, covering all six statuses with matching history.

## Repository structure

```text
JobHunt/
  backend/
    JobHunt.sln
    dotnet-tools.json
    JobHunt.Api/
      Controllers/
      Services/
      DataAccess/
      Entities/
      Models/
        Requests/JobApplications/
        Requests/Analytics/
        Requests/Interviews/
        Responses/JobApplications/
        Responses/Analytics/
        Responses/Interviews/
      Data/
        Configurations/
        Migrations/
  frontend/
    jobhunt-web/
      src/app/
        layout/application-layout/
        features/job-applications/
          pages/
            job-application-list/
            job-application-details/
          components/job-application-form/
          services/
          models/
        features/analytics/
          pages/application-dashboard/
          services/
          models/
        features/interviews/
          components/
            application-interviews/
            interview-form/
            upcoming-interviews/
          services/
          models/
      package.json
      package-lock.json
      proxy.conf.json
  scripts/
    Seed-SampleApplications.sql
    Test-JobApplicationFlow.ps1
    Test-JobApplicationDetails.ps1
    Test-UpdateJobApplicationFlow.ps1
    Test-ChangeJobApplicationStatusFlow.ps1
    Test-JobApplicationFiltering.ps1
    Test-ApplicationAnalytics.ps1
    Test-JobApplicationInterviews.ps1
  .gitignore
  README.md
```

Commit source code, migrations, the local .NET tool manifest, and package-lock.json.
Build output, installed dependencies, Visual Studio user settings, environment files,
and local database files are excluded by .gitignore. Database records are not part
of the repository; another checkout creates the schema using migrations and can
optionally run the fictional sample-data script.

To add these examples to a new local database (safe to re-run without duplicates):

```powershell
sqlcmd -S localhost -d JobHunt -E -C -b -i scripts/Seed-SampleApplications.sql
```

## Open in your editors

- Visual Studio 2026: open `backend/JobHunt.sln`. Set JobHunt.Api as the startup project
  and select the `http` launch profile. Press F5. API URL: http://localhost:5077.
- VS Code: open `frontend/jobhunt-web`. Run `npm install` on a fresh checkout, then
  `npm start`. Frontend URL: http://localhost:4200.

The Angular development proxy forwards `/api/**` to port 5077. It is a development
configuration; deployment routing will be configured when we reach that milestone.

## Database

Confirmed configuration: default SQL Server instance `localhost`, database `JobHunt`,
Windows authentication. The connection string is in `appsettings.Development.json`.
TrustServerCertificate is enabled for the local SQL Server development certificate.
No SQL passwords are stored. Production configuration is intentionally not supplied.

The database and initial migration have already been applied on this machine.
To prepare another machine with SQL Server installed, use PowerShell:

```powershell
cd backend
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project JobHunt.Api
```

The server does not automatically create or migrate databases at startup.

| Table | Contents |
| --- | --- |
| JobApplications | Current application details and status |
| JobApplicationStatusHistory | Initial status, event date, and UTC recording time |
| __EFMigrationsHistory | EF Core's record of applied schema migrations |

ApplicationDate and OccurredOn use calendar dates. CreatedAtUtc, UpdatedAtUtc, and
RecordedAtUtc use UTC timestamps. Initial history keeps the actual supplied application
date instead of pretending the application occurred when the record was created.
This first version validates today using the Asia/Kolkata calendar; timezone preferences
can be introduced with user accounts.

## Backend flow

```text
HTTP request
  -> JobApplicationsController
  -> JobApplicationService (business rules, initial status, response mapping)
  -> JobApplicationDataAccess (LINQ database queries and persistence)
  -> JobHuntDbContext (EF Core)
  -> SQL Server
```

Database LINQ stays in DataAccess. The service also uses LINQ Select to map already-loaded
entities to response models; that is in-memory transformation, not a database query.

- Models/Requests: values accepted from Angular.
- Models/Responses: values returned to Angular.
- Entities: classes mapped to database tables.
- Data/Configurations: columns, lengths, keys, relationships, and indexes.
- Data/Migrations: versioned schema changes generated by EF Core.

The API supports GET `/api/applications`, POST `/api/applications`, and GET
`/api/applications/{jobApplicationId}`. Creation returns HTTP 201 with a Location
header pointing to the saved record. New applications always start as Applied.
GET `/api/health` reports process availability, not database readiness.

## Frontend flow

```text
JobApplicationFormComponent -> submitted event
  -> JobApplicationListComponent
  -> JobApplicationService (HttpClient)
  -> backend API
  -> list refresh and success message
```

Feature folders contain pages, components, services, and TypeScript models. The shared
layout lives under layout/. Reactive forms handle input validation. Signals hold page
state. A native modal dialog handles focus containment and Escape dismissal.

## Run from terminals

```powershell
dotnet run --project backend/JobHunt.Api --launch-profile http
```

In a separate terminal:

```powershell
cd frontend/jobhunt-web
npm start
```

## Verification

```powershell
dotnet build backend/JobHunt.sln
cd frontend/jobhunt-web
npm run build
```

With the API running, from the workspace root:

```powershell
./scripts/Test-JobApplicationFlow.ps1
./scripts/Test-JobApplicationDetails.ps1
./scripts/Test-UpdateJobApplicationFlow.ps1
./scripts/Test-ChangeJobApplicationStatusFlow.ps1
./scripts/Test-JobApplicationFiltering.ps1
./scripts/Test-ApplicationAnalytics.ps1
./scripts/Test-JobApplicationInterviews.ps1
```

The flow integration check creates uniquely named temporary records, checks HTTP validation,
reads them through the API, verifies their SQL history, and deletes only its own records.
It requires the confirmed local SQL Server and Windows authentication configuration.
The details check reads the seeded samples and verifies complete chronological
history, saved notes, and responses for missing or invalid identifiers.
Browser visual and interaction checks were unavailable because browser access was denied.

## Next learning checkpoint

Walk through one real request from the form to SQL Server using a Visual Studio breakpoint
in JobApplicationsController, then JobApplicationService, then JobApplicationDataAccess.
The details page is under `features/job-applications/pages/job-application-details`
and uses `/applications/:jobApplicationId`. The single-record API returns
`{ application, statusHistory }`, using dedicated detail and history response models.
DataAccess uses `Include` to fetch related history; the service maps entities to responses.
Existing list and create response shapes remain the same. No schema changes are required.
Editing uses `PUT /api/applications/{jobApplicationId}` and UpdateJobApplicationRequest.
Create and update models share JobApplicationFieldsRequest for consistent field validation.
DataAccess loads a tracked entity and its history; EF Core saves changes together.
The edit verification script uses a disposable record and checks status preservation,
timeline corrections, rejected changes, and optional-field clearing.
Status changes use POST `/api/applications/{jobApplicationId}/status-changes` with
`{ status, occurredOn }` and ChangeJobApplicationStatusRequest. The Angular dialog lives
in `components/job-application-status-form`. Same-day changes are allowed; ties are ordered
by history identifier. Any different named stage is permitted, including reopening an
application; this version does not impose a rigid stage progression. The status check
uses only a disposable record and verifies successful changes and rejected requests.
The list endpoint accepts search, status, source, pageNumber (default 1), and pageSize
(default 10, maximum 100). Search checks company OR role; supplied filters combine with
AND. Source is an exact match. Text matching follows the SQL Server database collation.
The endpoint now returns a page envelope rather than a bare array:

```json
{
  "items": [],
  "totalApplications": 0,
  "matchingApplications": 0,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 0,
  "availableSources": []
}
```

DataAccess builds the LINQ query with Where, CountAsync, OrderByDescending,
ThenByDescending, Skip, and Take. Pagination executes in SQL. Source options come from
all saved applications, independently of filters. Out-of-range pages return no items;
the frontend returns to the last available page if the result set shrinks.
Verification uses 13 disposable records to check pages, combined filters, counts,
source options, empty results, and invalid query parameters, then removes those records.
No database migration is required for these query changes.

## Analytics dashboard

Open **Dashboard** in the sidebar or visit `http://localhost:4200/dashboard`.
Restart the backend after pulling these changes. No database migration is required.

`GET /api/analytics/applications` accepts optional `startDate` and `endDate` query
parameters in `yyyy-MM-dd` format. Both boundaries are inclusive and filter
**ApplicationDate**, not the dates of status changes. Either boundary can be omitted;
omitting both returns all saved applications. Reversed or invalid dates return HTTP 400.

All cards and breakdowns use this selected application cohort:

- Total applications: number of applications in the selected range.
- Applications this month: applications in that range also applied in the current
  calendar month, calculated using India time (Asia/Kolkata).
- Applications reaching interview: applications with at least one Interview history
  entry, regardless of the current status or when that interview occurred. Repeated
  interviews count once; an Offer without an Interview entry does not count.
- Interview conversion: reached-interview applications divided by total applications,
  multiplied by 100 and rounded to one decimal place. Empty ranges return zero.
- Current-status breakdown: all six statuses, including zero counts.
- Source breakdown: counts by source, with missing/blank sources shown as Unspecified.

Backend flow: `ApplicationAnalyticsController` → `ApplicationAnalyticsService` →
`ApplicationAnalyticsDataAccess` → `JobHuntDbContext` → SQL Server. Models are under
`Models/Requests/Analytics` and `Models/Responses/Analytics`; database result models
are under `DataAccess/Results`. DataAccess uses Where, CountAsync, Any, and GroupBy.
EF Core translates Any into SQL EXISTS, avoiding duplicate counts from joined history.
GroupBy computes counts in SQL. The service calculates the percentage and maps the
small aggregate results into responses; it does not load every application.
Queries execute sequentially because one DbContext cannot execute concurrent queries.

Angular files live under `features/analytics/{models,services,pages}`. The dashboard
uses a reactive date form, a lazy route, loading/error/empty states, and labeled count
bars. Applying dates cancels the previous HTTP request to prevent stale results. Refresh
reloads the applied range; All time clears both boundaries. Dashboard figures refresh
when opening the page or pressing Refresh, rather than polling for changes.

`Test-ApplicationAnalytics.ps1` checks inclusive boundaries, repeat interviews, rejection
after an interview, offers without interviews, breakdown totals, empty/open ranges, date
validation, and independent SQL totals. It removes only its own uniquely marked records.
Browser visual and interaction testing remains unverified because the saved browser
permission for localhost was denied.

## Interview rounds

Open an application and use **Interview rounds → Add round**. Each application can
have multiple rounds with a name, date/time, Online/Phone/In person mode, meeting link,
location or phone details, preparation notes, and feedback. **Edit round** can reschedule
an interview, update feedback, clear optional fields, or mark it cancelled.
Cancelled rounds remain visible in application details for reference.

Dates are entered and displayed in India time (IST, UTC+05:30). Angular sends an explicit
offset, for example `2026-10-12T14:30+05:30`; the backend stores UTC as
`2026-10-12T09:00:00Z`. Historical rounds are allowed. The interview's India calendar date
must be on or after the application date. Editing the application date cannot move it
beyond a saved interview, including cancelled rounds. Scheduling/editing interviews
does not automatically change application status or add status history; update the
status separately when the stage actually changes.

The dashboard shows the next 10 future, non-cancelled rounds in chronological order,
with a total count and links to their applications. This section includes all applications
and is independent of the analytics application-date filter. Past rounds are still
available in application details. Dashboard Refresh reloads both analytics and interviews.

API endpoints:

- `GET /api/applications/{jobApplicationId}/interviews`: all rounds, earliest first.
- `POST /api/applications/{jobApplicationId}/interviews`: create a round (HTTP 201).
- `PUT /api/applications/{jobApplicationId}/interviews/{interviewId}`: edit a round.
- `GET /api/interviews/upcoming`: `{ items, totalUpcomingInterviews }`, maximum 10 items.

Create/update uses `SaveJobApplicationInterviewRequest`. Required fields: `roundName`
(maximum 150), `scheduledAt` (ISO date/time with timezone offset), and `mode` (`Online`,
`Phone`, or `InPerson`). Optional fields: `meetingUrl` (HTTP/HTTPS, maximum 2048),
`location` (200), `preparationNotes` (5000), `feedback` (5000), and `isCancelled` (false
by default). Missing parents/rounds and attempts to edit another application's round
return 404; invalid fields return 400. Responses include UTC scheduled/created/updated times.

Backend flow: `JobApplicationInterviewsController` → `JobApplicationInterviewService` →
`JobApplicationInterviewDataAccess` → `JobHuntDbContext`. LINQ filtering and sorting
execute in DataAccess; the service validates the application/date relationship and
maps responses. Frontend components, models, and service live under `features/interviews`.

The `AddJobApplicationInterviews` migration creates `JobApplicationInterviews` with
an identity primary key and a foreign key to `JobApplications`. Deleting an application
cascades to its rounds. Indexes cover the application foreign key and cancelled/scheduled
time for upcoming queries. Existing application and status-history tables are preserved.
Apply it on a fresh checkout with:

```powershell
cd backend
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project JobHunt.Api
```

`Test-JobApplicationInterviews.ps1` verifies create/list/edit, rescheduling, feedback,
optional-field clearing, IST midnight conversion, cancellation, upcoming ordering/limits,
validation, round ownership, application chronology, unchanged status/history, and SQL
persistence. Temporary applications and their rounds are removed by guarded cleanup.
