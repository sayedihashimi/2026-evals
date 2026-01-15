using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.Logging;

namespace FunctionApp1.HealthChecks;

public sealed class HealthCheckTimerFunction(
    HealthCheckService service,
    ILogger<HealthCheckTimerFunction> logger)
{
    [Function(nameof(HealthCheckTimerFunction))]
    public async Task RunAsync(
        [TimerTrigger("%HealthCheck:Schedule%", RunOnStartup = true)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Timer triggered. ScheduleStatusLast={Last} ScheduleStatusNext={Next} IsPastDue={IsPastDue}",
            timer.ScheduleStatus?.Last,
            timer.ScheduleStatus?.Next,
            timer.IsPastDue);

        await service.RunOnceAsync(cancellationToken);
    }
}
