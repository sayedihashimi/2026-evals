using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogFileUploader;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Define command line options
        var directoryOption = new Option<string?>(
            "--directory", 
            "-d")
        {
            Description = "Path to the directory containing log files. Defaults to %AppData%/MyStorageApp/logs/"
        };

        var patternOption = new Option<string>(
            "--pattern", 
            "-p")
        {
            Description = "File pattern to match (e.g., *.log, *.txt)",
            DefaultValueFactory = _ => "*.log"
        };

        var deleteOption = new Option<bool>(
            "--delete")
        {
            Description = "Delete files after successful upload",
            DefaultValueFactory = _ => true
        };

        var dryRunOption = new Option<bool>(
            "--dry-run")
        {
            Description = "Simulate the operation without uploading or deleting files",
            DefaultValueFactory = _ => false
        };

        var verboseOption = new Option<bool>(
            "--verbose", 
            "-v")
        {
            Description = "Enable verbose (debug) logging",
            DefaultValueFactory = _ => false
        };

        var rootCommand = new RootCommand("Upload log files to Azure Blob Storage")
        {
            directoryOption,
            patternOption,
            deleteOption,
            dryRunOption,
            verboseOption
        };

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var directory = parseResult.GetValue(directoryOption);
            var pattern = parseResult.GetValue(patternOption) ?? "*.log";
            var delete = parseResult.GetValue(deleteOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var verbose = parseResult.GetValue(verboseOption);

            var exitCode = await RunAsync(directory, pattern, delete, dryRun, verbose, cancellationToken);
            return exitCode;
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunAsync(string? directory, string pattern, bool delete, bool dryRun, bool verbose, CancellationToken cancellationToken)
    {
        // Validate pattern to prevent directory traversal
        if (!IsValidFilePattern(pattern))
        {
            Console.Error.WriteLine($"Invalid file pattern: {pattern}. Pattern cannot contain path separators or directory traversal.");
            return 1;
        }

        // Resolve directory path
        var directoryPath = ResolveDirectoryPath(directory);

        // Build the host with proper disposal
        using var host = CreateHostBuilder(verbose).Build();
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
            logger.LogDebug("Verbose logging enabled");

            // Get the uploader service and run
            var uploader = host.Services.GetRequiredService<ILogFileUploader>();
            var result = await uploader.UploadFilesAsync(
                directoryPath,
                pattern,
                delete,
                dryRun);

            // Log summary
            logger.LogInformation(
                "Summary: {Processed} processed, {Uploaded} uploaded, {Skipped} skipped, {Deleted} deleted, {Failed} failed",
                result.FilesProcessed,
                result.FilesUploaded,
                result.FilesSkipped,
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

    /// <summary>
    /// Validates that the file pattern is safe and doesn't contain path traversal.
    /// </summary>
    private static bool IsValidFilePattern(string pattern)
    {
        // Reject patterns with path separators or directory traversal
        if (pattern.Contains('/') || pattern.Contains('\\') || pattern.Contains(".."))
        {
            return false;
        }

        // Reject empty or whitespace-only patterns
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        // Only allow valid glob characters
        return SafePatternRegex().IsMatch(pattern);
    }

    [GeneratedRegex(@"^[\w\-.*?\[\]]+$")]
    private static partial Regex SafePatternRegex();

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

    private static IHostBuilder CreateHostBuilder(bool verbose)
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

                // Override minimum level if verbose mode is enabled
                if (verbose)
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
            });
    }
}
