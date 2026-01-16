using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Azure.Communication.Email;
using FunctionApp1.HealthChecks;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services
    .AddOptions<HealthCheckOptions>()
    .Bind(builder.Configuration.GetSection(HealthCheckOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(HealthCheckService.HttpClientName)
    .ConfigureHttpClient((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<HealthCheckOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FunctionApp1-HealthCheck/1.0");
    });

builder.Services.AddDbContext<HealthCheckDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var raw = configuration.GetConnectionString("HealthChecks") ?? "Data Source=healthchecks.db";
    var absoluteDbPath = Path.Combine(AppContext.BaseDirectory, "healthchecks.db");
    var connectionString = HealthCheckConnectionString.RewriteToAbsoluteSqlitePath(raw, absoluteDbPath);
    options.UseSqlite(connectionString);
});

builder.Services.AddSingleton(sp =>
{
    var emailOptions = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
    return new EmailClient(emailOptions.ConnectionString);
});

builder.Services.AddScoped<HealthCheckService>();

var app = builder.Build();

app.Run();
