using System.CommandLine;
using ImageQueueProcessor;
using ImageQueueProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Define CLI using System.CommandLine first (to support --help without config)
var dryRunOption = new Option<bool>(
    name: "--dry-run",
    description: "Simulate the operation without making any changes");

var folderOption = new Option<string>(
    name: "--folder",
    description: "The folder containing images to enqueue")
{
    IsRequired = true
};

var patternOption = new Option<string>(
    name: "--pattern",
    getDefaultValue: () => "*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp",
    description: "File pattern(s) to match, separated by semicolons (e.g., *.png;*.jpg)");

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

// Check if it's a help request - if so, just invoke and exit without checking config
if (args.Length == 0 || args.Contains("--help") || args.Contains("-h") || args.Contains("-?"))
{
    return await rootCommand.InvokeAsync(args);
}

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
    return 1;
}

// Build host for DI
var host = Host.CreateDefaultBuilder(args)
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

var serviceProvider = host.Services;

// Set up handlers
enqueueCommand.SetHandler(async (string folder, string pattern, bool dryRun) =>
{
    var enqueueService = serviceProvider.GetRequiredService<IEnqueueService>();
    var success = await enqueueService.EnqueueImagesAsync(folder, pattern, dryRun);
    Environment.ExitCode = success ? 0 : 2;
}, folderOption, patternOption, dryRunOption);

processCommand.SetHandler(async (bool dryRun) =>
{
    var processService = serviceProvider.GetRequiredService<IProcessService>();
    var success = await processService.ProcessQueueAsync(dryRun);
    Environment.ExitCode = success ? 0 : 2;
}, dryRunOption);

// Execute
return await rootCommand.InvokeAsync(args);
