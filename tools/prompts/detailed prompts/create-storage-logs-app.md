Prompt: Add .NET 10 Console App to Upload Log Files to Azure Blob Storage

You are modifying an EXISTING repository that already contains a .NET solution file (.sln or .slnx).

Your task is to CREATE a NEW .NET 10 console application named LogFileUploader, add it to the existing solution, and implement the full functionality described below so the project builds and runs with no further interaction or clarification.

--------------------------------------------------
High-Level Goal

Create a console app that:

1. Scans a directory for log files
2. Uploads each file to Azure Blob Storage
3. Deletes the local file ONLY after a successful upload
4. Skips invalid, locked, or oversized files
5. Logs progress and errors
6. Exits with a non-zero exit code if ANY upload or delete fails

--------------------------------------------------
Framework & Libraries

- Target framework: net10.0
- Language: C# (latest)
- Use Microsoft.Extensions.Hosting (Generic Host)
- Use Azure.Storage.Blobs
- Use System.CommandLine 2.0.2 (stable)
- Use Polly 8.x for retry logic
- Use async I/O everywhere
- Cross-platform (PowerShell Core friendly)

--------------------------------------------------
Project Creation

- Project name: LogFileUploader
- Preferred path: src/LogFileUploader/LogFileUploader.csproj
- Follow existing repo conventions if different
- Add the project to the existing solution file
- Ensure dotnet build succeeds

--------------------------------------------------
Default Log Directory

If --directory is NOT specified, default to:

%AppData%/MyStorageApp/logs/

Resolve %AppData% using:
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

Always combine paths using Path.Combine.

--------------------------------------------------
Command Line Interface (System.CommandLine 2.0.2)

Supported options:

--directory    Directory containing log files (optional)
--pattern      File pattern (default: *.log)
--dry-run      Do not upload or delete, log only
--delete       Whether to delete after upload (default: true)
--verbose, -v  Enable Debug-level logging

Validation rules:

- Directory must exist and be readable
- File pattern MUST:
  - Not contain path separators (/ or \)
  - Not contain ..
  - Be validated using a [GeneratedRegex]
- Program class MUST be declared as partial to support [GeneratedRegex]

--------------------------------------------------
Configuration Files

appsettings.json (checked in, NO secrets):

{
  "BlobUpload": {
    "ContainerName": "logs",
    "BlobPrefix": "ingest/",
    "ContentType": "text/plain",
    "Overwrite": false,
    "MaxRetryAttempts": 5,
    "RetryDelayMilliseconds": 1000,
    "MinFileSizeBytes": 1,
    "MaxFileSizeBytes": 104857600
  }
}

appsettings.json.user (NOT checked in, must be ignored via .gitignore):

{
  "BlobUpload": {
    "ConnectionString": "REPLACE_ME"
  }
}

--------------------------------------------------
Blob Naming Convention

Uploaded blob name format:

{BlobPrefix}{originalFileName}

- Preserve the original filename exactly
- Do NOT add date-based folders

--------------------------------------------------
Upload Behavior

For each matched file:

1. Validate file:
   - Exists
   - Size >= MinFileSizeBytes
   - Size <= MaxFileSizeBytes
   - Not locked (detect file sharing violations)
2. Open file as read-only stream
3. Upload using BlobContainerClient / BlobClient
4. Apply retry policy using Polly with exponential backoff
   - Retry on HTTP status codes: 408, 429, 500, 502, 503, 504
5. On successful upload:
   - Delete the file (unless --dry-run or --delete=false)
6. On failure:
   - Do NOT delete the file
   - Record the error
7. Continue processing remaining files

--------------------------------------------------
Container Handling

- Call CreateIfNotExistsAsync()
- Container creation must be thread-safe
  - Use SemaphoreSlim with double-checked locking
- Respect Overwrite setting:
  - false = existing blob is an error
  - true  = overwrite allowed

--------------------------------------------------
Retry Logic (Required)

- Use Polly ResiliencePipeline
- Retry settings must be configurable via BlobUpload settings
- Log each retry attempt

--------------------------------------------------
Exit Codes

0 = All files uploaded successfully
1 = Invalid arguments or configuration
2 = One or more uploads or deletes failed

--------------------------------------------------
Logging

Use ILogger to log:

- Startup configuration summary
- Each file processed
- Upload attempts and successes
- Delete attempts and successes
- Errors with full exception details
- Retry attempts
- Final summary

--verbose enables Debug-level logging

--------------------------------------------------
Data Structures

BlobUploadSettings class using IOptions<T>

UploadResult MUST be immutable:

public record UploadResult(
    bool Success,
    int FilesProcessed,
    int FilesUploaded,
    int FilesDeleted,
    int FilesFailed,
    int FilesSkipped,
    IReadOnlyList<string> Errors);

--------------------------------------------------
Suggested Project Structure

LogFileUploader/
  Program.cs                  (partial, CLI + host lifecycle, host disposed correctly)
  LogFileUploaderService.cs   (core upload logic)
  ILogFileUploader.cs
  BlobUploadSettings.cs
  LogFileUploader.csproj
  appsettings.json
  README.md

--------------------------------------------------
Required NuGet Packages

- Azure.Storage.Blobs
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging.Console
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.Configuration.EnvironmentVariables
- System.CommandLine (2.0.2)
- Polly (8.x)

--------------------------------------------------
README Requirements

Document:
- Purpose of the tool
- Default directory behavior
- Configuration files and secrets handling
- Example commands
- Exit codes

--------------------------------------------------
Verification (MUST PASS)

dotnet build
dotnet run --project src/LogFileUploader
dotnet run --project src/LogFileUploader -- --help

--------------------------------------------------
Deliverables

- New project added to solution
- appsettings.json committed
- appsettings.json.user created but ignored
- Full retry, validation, logging, and exit behavior implemented
- Build succeeds with no TODOs or warnings
