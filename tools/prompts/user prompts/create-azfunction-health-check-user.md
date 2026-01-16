I have an existing Azure Functions project using the .NET isolated worker model.

I want to add a new timer-triggered Azure Function that runs on a schedule (cron-based).  
The schedule should be configurable using application settings.

On each run, the function should:
- Call a configurable URL to perform a basic HTTP health check.
- Treat the check as successful if the response status code is 2xx.
- Log whether the check succeeded or failed.

Use HttpClient (via dependency injection, not new HttpClient).

Store the result of each run (success or failure) in a database using Entity Framework Core.
SQLite is fine to start with.

The function should work both locally (local.settings.json) and in Azure App Settings.
