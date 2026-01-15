using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogFileUploader;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Define command line options
        var directoryOption = new Option<string?>(
            name: "--directory",
            description: "Path to the directory containing log files. Defaults to %AppData%/MyStorageApp/logs/");
        directoryOption.AddAlias("-d");

        var patternOption = new Option<string>(
            name: "--pattern",
            getDefaultValue: () => "*.log",
            description: "File pattern to match (e.g., *.log, *.txt)");
        patternOption.AddAlias("-p");

        var deleteOption = new Option<bool>(
            name: "--delete",
            getDefaultValue: () => true,
            description: "Delete files after successful upload");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            getDefaultValue: () => false,
            description: "Simulate the operation without uploading or deleting files");

        var rootCommand = new RootCommand("Upload log files to Azure Blob Storage")
        {
            directoryOption,
            patternOption,
            deleteOption,
            dryRunOption
        };

        rootCommand.SetHandler(async (directory, pattern, delete, dryRun) =>
        {
            var exitCode = await RunAsync(directory, pattern, delete, dryRun);
            Environment.ExitCode = exitCode;
        }, directoryOption, patternOption, deleteOption, dryRunOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> RunAsync(string? directory, string pattern, bool delete, bool dryRun)
    {
        // Resolve directory path
        var directoryPath = ResolveDirectoryPath(directory);

        // Build the host
        var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            // Validate directory
            if (!Directory.Exists(directoryPath))
            {
                logger.LogError("Directory does not exist: {Directory}", directoryPath);
                return 1;
            }

            // Check if directory is readable
            try
            {
                Directory.GetFiles(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogError("Cannot read directory (access denied): {Directory}", directoryPath);
                return 1;
            }
            catch (IOException ex)
            {
                logger.LogError(ex, "Cannot read directory: {Directory}", directoryPath);
                return 1;
            }

            logger.LogInformation("Log File Uploader starting...");
            logger.LogInformation("Directory: {Directory}", directoryPath);
            logger.LogInformation("Pattern: {Pattern}", pattern);
            logger.LogInformation("Delete after upload: {Delete}", delete);
            logger.LogInformation("Dry run: {DryRun}", dryRun);

            // Get the uploader service and run
            var uploader = host.Services.GetRequiredService<ILogFileUploader>();
            var result = await uploader.UploadFilesAsync(
                directoryPath,
                pattern,
                delete,
                dryRun);

            // Log summary
            logger.LogInformation(
                "Summary: {Processed} processed, {Uploaded} uploaded, {Deleted} deleted, {Failed} failed",
                result.FilesProcessed,
                result.FilesUploaded,
                result.FilesDeleted,
                result.FilesFailed);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    logger.LogError("Error: {Error}", error);
                }
                return 1;
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Configuration error: {Message}", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error: {Message}", ex.Message);
            return 1;
        }
    }

    private static string ResolveDirectoryPath(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
        {
            return Path.GetFullPath(directory);
        }

        // Default to %AppData%/MyStorageApp/logs/
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "MyStorageApp", "logs");
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                config.AddJsonFile("appsettings.json.user", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configure options
                services.Configure<BlobUploadSettings>(
                    context.Configuration.GetSection(BlobUploadSettings.SectionName));

                // Register services
                services.AddSingleton<ILogFileUploader, LogFileUploaderService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            });
    }
}
