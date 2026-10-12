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

Current scope is a local tracker with applications, status history, analytics,
interview rounds, account registration/login, JWT sessions, and per-user data access.
AI is a later milestone. Six fictional applications were added
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

The database and migrations, including Identity and session/ownership tables, have been applied on this machine.
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
| JobApplicationInterviews | Interview rounds |
| AspNetUsers and supporting Identity tables | Accounts and Identity storage |
| RefreshTokens | Hashed refresh tokens and session rotation/revocation history |
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

With the API running, from the workspace root, the full authentication check creates disposable
accounts and runs the existing feature checks with an authenticated user:

```powershell
# Use Python 3 with the standard library, or the bundled Codex Python runtime.
python scripts/Test-Authentication.py
./scripts/Test-UserRegistration.ps1
```

For individual tracker checks, set JOBHUNT_TEST_ACCESS_TOKEN to a login access token or pass
`-AccessToken` to each command below. The details check needs sample records owned by that user.

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

## Authentication

Open `http://localhost:4200/register` to create an account, then sign in at `/login`.
The Angular route guards restore sessions from the refresh cookie after a page reload.
The interceptor attaches access tokens to tracker requests, refreshes expired tokens, and
retries once on HTTP 401. Refresh calls are shared within each tab and serialized across
tabs through Web Locks when supported. Login/logout changes notify other tabs through
BroadcastChannel. Access tokens live in memory; neither token is stored in browser web storage.

| Endpoint | Behavior |
| --- | --- |
| POST /api/auth/register | Creates an account; HTTP 201 with public user fields |
| POST /api/auth/login | Verifies password; returns access token, expiry, user; sets refresh cookie |
| POST /api/auth/refresh | Rotates refresh token and issues a new access token |
| POST /api/auth/logout | Revokes the login session and clears refresh cookie; HTTP 204 |
| GET /api/auth/me | Requires bearer token; returns public account fields |

Login, refresh, and logout require `X-JobHunt-CSRF: 1`. Browser Origin must match the API
origin or an explicitly configured Auth:AllowedOrigins entry. No cross-origin CORS policy
is enabled. Development permits the Angular localhost origin; deploy UI/API under one origin.
Auth endpoints have a per-IP limit of 60 requests per minute and return HTTP 429 when exceeded.
Five incorrect passwords temporarily lock the account for Identity's default five minutes.

Access tokens are signed HS256 JWTs with a 10-minute lifetime. The backend validates signature,
issuer, audience, algorithm, and expiry, with five seconds of clock tolerance. Refresh sessions
last seven days from login, use random tokens stored as SHA-256 hashes, and rotate on every
refresh. Reusing a rotated token revokes its whole session family. SQL row locks serialize
simultaneous refreshes. Refresh also rejects expired sessions, locked users, and changed
Identity security stamps. Logout revokes refresh tokens; existing access tokens can remain
valid until their short expiry. Email verification and password reset email delivery are not configured.

The refresh cookie is HttpOnly, SameSite Strict, scoped to /api/auth, and Secure outside
Development. Production requires HTTPS and a protected signing secret. Auth responses are not cached.

### Signing key setup

A random development key has been saved in local .NET user secrets, outside this repository.
On another machine, configure a strong random key using `dotnet user-secrets set Jwt:SigningKey`
for backend/JobHunt.Api, or set `Jwt__SigningKey` in the environment. Issuer, audience and token
lifetime are in appsettings.json. The API fails startup if the signing key is missing or too short.
Do not commit the secret. Production uses protected environment/secret-provider configuration.

### Application ownership

New applications receive the authenticated user's ID server-side. EF query filters scope
applications, status history, interviews, counts, source filters, upcoming rounds and analytics
to that user. Requests for another user's application or round return HTTP 404. All tracker
endpoints require authentication; /api/health remains public.

The seven legacy applications and their 17 history entries were preserved with null owners.
They are hidden from every account until an explicit local assignment. Register your own
account, then assign only unowned applications to it:

```powershell
./scripts/Assign-LegacyApplications.ps1 -Email 'your-email@example.com'
```

This uses the existing local SQL Server Windows-authentication setup. It never reassigns
already-owned applications. Ownership is not assigned automatically to the first registrant.

### Registration details

`POST /api/auth/register` accepts an email address (maximum 256 characters) and a
password (maximum 128 characters). Identity requires unique emails and passwords
of at least 10 characters containing uppercase, lowercase, a digit, and a non-alphanumeric
character. Email is also used as the username, so Identity's default username-character
rules apply. Duplicate emails, including different casing, return HTTP 400 with an
Email validation error. Invalid fields and weak passwords also return HTTP 400 with
field-specific validation messages.

Example local learning request:

```json
{
  "email": "learner@example.com",
  "password": "StudyPass!2026"
}
```

Successful registration returns HTTP 201 with `{ id, email, emailConfirmed }`.
EmailConfirmed is false because email verification has not been implemented. Registration
creates the account without logging in, issuing tokens, or assigning tracker records.
Existing application APIs now require a bearer token and enforce ownership.

Flow: `AuthController` → `AuthService` → `UserManager<ApplicationUser>` → Identity's
EF Core store → `JobHuntDbContext` → SQL Server. Identity performs account/password
validation and hashing; there is no custom password hashing or duplicate account repository.
The service returns only public account fields. The normalized username unique index
also prevents simultaneous registrations from saving the same email twice.

The AddIdentityTables and AddAuthenticationSessionsAndOwnership migrations have been applied
to the local database. Use the existing database update command on a fresh checkout.

With the backend running, verify this feature using PowerShell 7:

```powershell
./scripts/Test-UserRegistration.ps1
```

This check verifies validation, duplicate and concurrent registration, safe responses,
SQL persistence, password verification against the stored Identity hash, and different
salted hashes for identical passwords. It removes only its own GUID-named test accounts.

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
with a total count and links to their applications. This section includes the signed-in user's applications
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
