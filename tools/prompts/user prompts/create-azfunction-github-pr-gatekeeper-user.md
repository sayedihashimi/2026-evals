I have an existing Azure Functions project using the .NET isolated worker model.

I want to add a timer-triggered Azure Function that runs every minute (on a configurable schedule).

On each run, the function should:
- Perform an HTTP health check against a configurable URL.
- Consider the check successful if the response is 2xx.
- Store the result (success or failure) in a database using Entity Framework Core (SQLite is fine).

If the health check fails:
- Save the failure to the database.
- Send an email notification with basic details about the failure.
- Do not send email when the check succeeds.

Use HttpClient via dependency injection.
Use Azure Communication Services to send the email.

The function should work both locally (local.settings.json) and when deployed to Azure.
