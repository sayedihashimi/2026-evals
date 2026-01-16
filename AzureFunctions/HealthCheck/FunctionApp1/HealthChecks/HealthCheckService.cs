using System.Diagnostics;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FunctionApp1.HealthChecks;

public sealed class HealthCheckService(
    IHttpClientFactory httpClientFactory,
    IOptions<HealthCheckOptions> healthCheckOptions,
    IOptions<EmailOptions> emailOptions,
    EmailClient emailClient,
    HealthCheckDbContext db,
    ILogger<HealthCheckService> logger)
{
    public const string HttpClientName = "HealthCheck";

    public async Task<HealthCheckResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var options = healthCheckOptions.Value;
        var checkedUrl = options.TargetUrl;
        var timestamp = DateTimeOffset.UtcNow;

        logger.LogInformation("Health check starting. Url={Url} TimestampUtc={TimestampUtc}", checkedUrl, timestamp);

        HealthCheckResult result;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, checkedUrl);
            var client = httpClientFactory.CreateClient(HttpClientName);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                result = new HealthCheckResult
                {
                    CheckedUrl = checkedUrl,
                    TimestampUtc = timestamp,
                    IsSuccess = true,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = null,
                };
            }
            else
            {
                result = new HealthCheckResult
                {
                    CheckedUrl = checkedUrl,
                    TimestampUtc = timestamp,
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = $"Non-success status code: {(int)response.StatusCode} ({response.ReasonPhrase})",
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new HealthCheckResult
            {
                CheckedUrl = checkedUrl,
                TimestampUtc = timestamp,
                IsSuccess = false,
                StatusCode = null,
                ErrorMessage = "Request timed out",
            };
        }
        catch (Exception ex)
        {
            result = new HealthCheckResult
            {
                CheckedUrl = checkedUrl,
                TimestampUtc = timestamp,
                IsSuccess = false,
                StatusCode = null,
                ErrorMessage = ex.Message,
            };

            logger.LogError(ex, "Health check execution failed. Url={Url}", checkedUrl);
        }

        db.Results.Add(result);
        await db.SaveChangesAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            await TrySendFailureEmailAsync(result, cancellationToken);
        }

        sw.Stop();
        logger.LogInformation(
            "Health check complete. Id={Id} Url={Url} Success={Success} StatusCode={StatusCode} DurationMs={DurationMs}",
            result.Id,
            result.CheckedUrl,
            result.IsSuccess,
            result.StatusCode,
            sw.ElapsedMilliseconds);

        return result;
    }

    private async Task TrySendFailureEmailAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;

        var subject = "Health Check Failure Detected";
        var body =
            $"Checked URL: {result.CheckedUrl}\n" +
            $"Timestamp (UTC): {result.TimestampUtc:O}\n" +
            $"Status code: {(result.StatusCode is null ? "(none)" : result.StatusCode.Value.ToString())}\n" +
            $"Error message: {(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "(none)" : result.ErrorMessage)}\n";

        var message = new EmailMessage(
            senderAddress: options.SenderAddress,
            recipientAddress: options.RecipientAddress,
            content: new EmailContent(subject)
            {
                PlainText = body,
            });

        try
        {
            logger.LogInformation(
                "Sending failure email. ResultId={Id} To={To} From={From}",
                result.Id,
                options.RecipientAddress,
                options.SenderAddress);

            EmailSendOperation op = await emailClient.SendAsync(WaitUntil.Completed, message, cancellationToken);

            logger.LogInformation(
                "Failure email sent. ResultId={Id} MessageId={MessageId} Status={Status}",
                result.Id,
                op.Id,
                op.Value.Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Failure email send canceled. ResultId={Id}",
                result.Id);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Failure email send failed (ACS). ResultId={Id}", result.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure email send failed. ResultId={Id}", result.Id);
        }
    }
}
