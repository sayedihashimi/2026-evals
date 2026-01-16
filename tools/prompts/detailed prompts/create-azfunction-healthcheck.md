You are adding a new Azure Function to an EXISTING Azure Functions project using the .NET isolated worker model (.NET 8 or later).

Goal:
Create a scheduled/cron job that runs every minute by default, but the schedule MUST be configurable via app settings (local.settings.json / Azure App Settings).

Behavior:
1) On each run, perform an HTTP health check to a target URL (configurable setting).
   - Default TargetUrl should be https://aspire.dev/ but MUST be configurable.
   - Use an injected HttpClient from IHttpClientFactory / AddHttpClient (do NOT new HttpClient()).
   - Consider the health check successful only if the HTTP call completes and returns a 2xx status code.
   - Use a short timeout (configurable or reasonable default like 5 seconds).
2) Record the result of EVERY run (success or failure) in a database using Entity Framework Core.
3) When a FAILURE is detected:
   - Persist the failure to the database.
   - Send an email notification.
   - Do NOT send email on success.

Email behavior:
- Use Azure Communication Services (ACS) Email SDK.
- Send a simple transactional email containing:
  - Subject: "Health Check Failure Detected"
  - Body (plain text is sufficient):
    - Checked URL
    - Timestamp (UTC)
    - Status code (if any)
    - Error message (if any)
- Send the email ONLY for failures.
- Log success or failure of the email send, but do not crash the function if email sending fails.

Persistence:
- Use Entity Framework Core with SQLite as the initial provider.
- The SQLite database file MUST be created and used from the application’s runtime output directory
  using an absolute path based on AppContext.BaseDirectory.
- Do NOT rely on copying a .db file to the output directory.
- Design the DbContext so the provider can be swapped later.
- Use EF Core Migrations (NOT EnsureCreated).

Configuration (strongly typed via IOptions<T>):
- HealthCheck:Schedule (default "0 */1 * * * *")
- HealthCheck:TargetUrl (default "https://aspire.dev/")
- HealthCheck:TimeoutSeconds (optional, default 5)
- ConnectionStrings:HealthChecks (SQLite, "Data Source=healthchecks.db")

Email settings:
- Email:ConnectionString (ACS Email connection string)
- Email:SenderAddress (verified sender in ACS)
- Email:RecipientAddress (default "report@example.com")

Data Model:
Create an entity HealthCheckResult with at least:
- Id (int, primary key)
- CheckedUrl (string)
- TimestampUtc (DateTimeOffset)
- IsSuccess (bool)
- StatusCode (int?, nullable)
- ErrorMessage (string?, nullable)

Implementation details:
- Create HealthCheckOptions and EmailOptions with validation.
- Create HealthCheckDbContext using DbContextOptions.
- In Program.cs:
  - Build the SQLite connection string using AppContext.BaseDirectory
  - Register DbContext with SQLite provider
  - Register HttpClient via AddHttpClient
  - Register ACS Email client
- Create a HealthCheckService that:
  - Executes the HTTP check
  - Persists results
  - Sends email on failure
- Keep the Azure Function class thin (delegate work to the service).

Azure Function:
- Create a TimerTrigger function (HealthCheckTimerFunction).
- Use a configurable schedule:
  - "%HealthCheck:Schedule%"
- Specify:
  - RunOnStartup = true
- Must work with local.settings.json and Azure App Settings.

Build & Migration requirements:
1) Ensure the project BUILDS successfully before creating migrations.
2) If dotnet-ef is not installed:
   - dotnet tool install --global dotnet-ef
3) Create migration:
   - dotnet ef migrations add InitialHealthCheckSchema
4) Apply migration:
   - dotnet ef database update

Correctness requirements:
- Use async/await everywhere.
- Pass CancellationToken to HttpClient, EF Core, and email send operations.
- Do not hard-code URLs, cron expressions, or email addresses.
- Failures MUST always be recorded in the database even if email sending fails.
- Code must compile and run locally.

Logging:
- Use ILogger in all layers.
- Log start/end of each run and duration.
- Log health check failures and email send attempts/results.

Deliverables:
- Program.cs changes
- New classes:
  - HealthCheckOptions
  - EmailOptions
  - HealthCheckDbContext
  - HealthCheckResult
  - HealthCheckService
  - HealthCheckTimerFunction
- Example local.settings.json showing email configuration
- Explicit dotnet-ef commands used

Quality bar:
Write production-quality .NET 8 isolated Azure Functions code. The final solution must build, apply EF Core migrations successfully, and send email notifications on health check failures.
