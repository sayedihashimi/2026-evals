using System.CommandLine;
using ImageQueueProcessor;
using ImageQueueProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Define CLI using System.CommandLine
var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "Simulate the operation without making any changes",
    DefaultValueFactory = _ => false
};

var folderOption = new Option<string>("--folder")
{
    Description = "The folder containing images to enqueue",
    Required = true
};

var patternOption = new Option<string>("--pattern")
{
    Description = "File pattern(s) to match, separated by semicolons (e.g., *.png;*.jpg)",
    DefaultValueFactory = _ => "*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp"
};

// Enqueue command
var enqueueCommand = new Command("enqueue", "Enqueue images from a local folder to Azure Storage Queue")
{
    folderOption,
    patternOption,
    dryRunOption
};

// Process command
var processCommand = new Command("process", "Process images from Azure Storage Queue, resize, and upload to Blob Storage")
{
    dryRunOption
};

// Root command
var rootCommand = new RootCommand("Image Queue Processor - Enqueue images to Azure Storage Queue and process them to Blob Storage")
{
    enqueueCommand,
    processCommand
};

// Set up handlers using SetAction (System.CommandLine 2.0.2 API)
enqueueCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var folder = parseResult.GetValue(folderOption)!;
    var pattern = parseResult.GetValue(patternOption) ?? "*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp";
    var dryRun = parseResult.GetValue(dryRunOption);

    var (success, exitCode) = await RunEnqueueAsync(folder, pattern, dryRun, cancellationToken);
    return exitCode;
});

processCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var dryRun = parseResult.GetValue(dryRunOption);

    var (success, exitCode) = await RunProcessAsync(dryRun, cancellationToken);
    return exitCode;
});

// Execute using Parse().InvokeAsync()
return await rootCommand.Parse(args).InvokeAsync();

// Helper methods for running commands
async Task<(bool Success, int ExitCode)> RunEnqueueAsync(string folder, string pattern, bool dryRun, CancellationToken cancellationToken)
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile("appsettings.json.user", optional: true, reloadOnChange: false)
        .Build();

    // Validate configuration
    var settings = new QueueProcessingSettings();
    configuration.GetSection("QueueProcessing").Bind(settings);

    if (string.IsNullOrWhiteSpace(settings.ConnectionString) || settings.ConnectionString == "REPLACE_ME")
    {
        Console.Error.WriteLine("ERROR: ConnectionString is not configured.");
        Console.Error.WriteLine("Please set QueueProcessing:ConnectionString in appsettings.json.user");
        return (false, 1);
    }

    // Build host for DI
    using var host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(builder =>
        {
            builder.Sources.Clear();
            builder.AddConfiguration(configuration);
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<QueueProcessingSettings>(context.Configuration.GetSection("QueueProcessing"));
            services.AddTransient<IEnqueueService, EnqueueService>();
            services.AddTransient<IProcessService, ProcessService>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        })
        .Build();

    var enqueueService = host.Services.GetRequiredService<IEnqueueService>();
    var success = await enqueueService.EnqueueImagesAsync(folder, pattern, dryRun, cancellationToken);
    return (success, success ? 0 : 2);
}

async Task<(bool Success, int ExitCode)> RunProcessAsync(bool dryRun, CancellationToken cancellationToken)
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile("appsettings.json.user", optional: true, reloadOnChange: false)
        .Build();

    // Validate configuration
    var settings = new QueueProcessingSettings();
    configuration.GetSection("QueueProcessing").Bind(settings);

    if (string.IsNullOrWhiteSpace(settings.ConnectionString) || settings.ConnectionString == "REPLACE_ME")
    {
        Console.Error.WriteLine("ERROR: ConnectionString is not configured.");
        Console.Error.WriteLine("Please set QueueProcessing:ConnectionString in appsettings.json.user");
        return (false, 1);
    }

    // Build host for DI
    using var host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(builder =>
        {
            builder.Sources.Clear();
            builder.AddConfiguration(configuration);
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<QueueProcessingSettings>(context.Configuration.GetSection("QueueProcessing"));
            services.AddTransient<IEnqueueService, EnqueueService>();
            services.AddTransient<IProcessService, ProcessService>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        })
        .Build();

    var processService = host.Services.GetRequiredService<IProcessService>();
    var success = await processService.ProcessQueueAsync(dryRun, cancellationToken);
    return (success, success ? 0 : 2);
}
