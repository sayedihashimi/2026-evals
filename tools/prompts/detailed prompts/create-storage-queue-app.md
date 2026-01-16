PROMPT FILE: Create .NET 10 app that enqueues images to Azure Storage Queue and processes queue to resize + upload to Azure Blob

You are modifying an EXISTING repository that already contains a .NET solution file (.sln or .slnx). Create a NEW .NET 10 console application project and add it to the existing solution. After generating the code, it MUST build and run successfully. If it doesn’t, iterate on the code until it works (no further user interaction).

============================================================
GOAL

Create a .NET 10 console app that supports TWO operations:

1) ENQUEUE:
   - Read image files from a local folder
   - Send each image as a message to an Azure Storage Queue
   - After a successful enqueue, delete the local image file
   - If the queue does not exist, create it

2) PROCESS:
   - Dequeue messages
   - For each message: “download” the image bytes from the message payload, resize the image to 50% width (maintain aspect ratio), and upload the resized image to Azure Blob Storage
   - Upload to a blob container named "resizedimages" by default (but configurable)
   - If the container does not exist, create it
   - Delete the queue message ONLY after successful upload (and only if not dry-run)

Include a CLI option: --dry-run
- In dry-run, do NOT enqueue messages, do NOT delete local files, do NOT upload blobs, and do NOT delete queue messages. Only log what WOULD happen.

============================================================
TECH REQUIREMENTS

- Target framework: net10.0
- Language: C# latest
- Use Generic Host (Microsoft.Extensions.Hosting) for DI, config, and logging
- Use Azure SDKs (do NOT shell out to az cli):
  - Azure.Storage.Queues
  - Azure.Storage.Blobs
- Use the LATEST stable System.CommandLine NuGet package (do not use older beta packages). Add as a PackageReference and write the Program using the current API.
- Ensure the app is cross-platform (no Windows-only imaging APIs). Use a cross-platform image library for resizing (for example: SixLabors.ImageSharp) and include the necessary NuGet reference.

============================================================
PROJECT CREATION / SOLUTION INTEGRATION

- Project name: ImageQueueProcessor (or a similar unique name if collision exists)
- Prefer location: src/ImageQueueProcessor/ImageQueueProcessor.csproj (follow repo conventions if different)
- Add the project to the existing solution (.sln or .slnx)
- Ensure: dotnet build succeeds

============================================================
CONFIGURATION (appsettings.json + appsettings.json.user)

- appsettings.json: checked in, NO secrets
- appsettings.json.user: NOT checked in, secrets only, must be added to .gitignore if not already ignored

Create stubs for BOTH files:

appsettings.json (checked in):
{
  "QueueProcessing": {
    "QueueName": "images",
    "ResizedImagesContainer": "resizedimages"
  }
}

appsettings.json.user (NOT checked in):
{
  "QueueProcessing": {
    "ConnectionString": "REPLACE_ME"
  }
}

Notes:
- Use this single ConnectionString for BOTH queue + blob clients (same storage account).
- Bind configuration using IOptions<T> pattern into a settings class (e.g., QueueProcessingSettings).
- Fail fast with a clear error if ConnectionString is missing/empty.

============================================================
CLI DESIGN (System.CommandLine)

Implement subcommands (recommended) so the tool is unambiguous:

image-tool enqueue --folder <path> [--pattern <glob>] [--dry-run]
image-tool process [--dry-run]

Details:
- Global option: --dry-run (applies to both subcommands)
- enqueue:
  - --folder (required): directory containing images
  - --pattern (optional): default to typical images, e.g. "*.png;*.jpg;*.jpeg" OR accept a single pattern and document it (either is fine, but be consistent)
- process:
  - no required args; it processes until the queue is empty, then exits

Also support --help and reasonable validation/error messages.

============================================================
ENQUEUE BEHAVIOR DETAILS

For each image file matched in the folder:
- Read bytes from disk
- Create a queue message whose payload contains:
  - Original file name
  - Image bytes encoded as Base64
  - Any other minimal metadata you need (JSON is fine)
- Enqueue the message
- If enqueue succeeds:
  - Delete local file (unless --dry-run)
- If enqueue fails:
  - Do NOT delete the local file
  - Record the error and continue to next file

Queue existence:
- Ensure the queue exists before sending messages (CreateIfNotExists).

IMPORTANT: Azure Storage Queue messages have size limits. Implement a safe check:
- If the message payload would exceed the queue limit, treat it as a failure:
  - Log an error explaining the file is too large for a queue message
  - Do NOT delete the local file
  - Continue processing other files
- The app must return a non-zero exit code if any file failed.

============================================================
PROCESS BEHAVIOR DETAILS

Processing loop:
- Ensure the queue exists (CreateIfNotExists)
- Dequeue messages in a loop until none remain
- For each message:
  - Deserialize payload (JSON)
  - Decode Base64 image bytes
  - Resize image to 50% of original width, maintaining aspect ratio
  - Choose output format reasonably:
    - Preserve original format if possible, or standardize to PNG/JPEG
    - Use the original filename when uploading, optionally with a suffix like "-50" before extension (document what you do)
  - Ensure the blob container exists (CreateIfNotExists) using the configured container name (default "resizedimages")
  - Upload the resized image bytes to the container
  - Only after successful upload:
    - Delete the queue message (unless --dry-run)
- If any message processing fails:
  - Log error with exception
  - Do NOT delete the queue message (unless you intentionally want poison-message behavior; keep it simple and safe: do not delete on failure)
  - Continue processing remaining messages
- Exit after queue is empty

============================================================
BLOB UPLOAD DETAILS

- Container name comes from config: QueueProcessing:ResizedImagesContainer (default resizedimages)
- Create container if missing
- Upload blob with a deterministic name derived from original filename (and optional suffix)
- Set content-type if feasible (nice-to-have)

============================================================
LOGGING

Use ILogger:
- On startup: log selected operation, folder/pattern if enqueue, dry-run status, queue/container names
- Per file: log enqueue/upload actions and outcomes
- On errors: log exception + context
- End summary: counts (files found, enqueued, deleted locally, messages processed, blobs uploaded, failures)

============================================================
EXIT CODES

- Exit 0 only if the entire run completed with no failures
- Exit non-zero if any failure occurred:
  - Bad args/config => exit 1
  - Any enqueue/process failures (including oversized message, upload failure, delete failure) => exit 2

============================================================
REQUIRED OUTPUTS / VERIFICATION

- Add/commit the new project (and appsettings.json stub)
- Create appsettings.json.user stub but ensure it is ignored (not committed)
- Ensure solution builds:
  - dotnet build
- Ensure the app runs and shows help:
  - dotnet run --project src/ImageQueueProcessor -- --help
- Provide example commands in a README.md for the project:
  - enqueue example
  - process example
  - explain appsettings.json.user secret requirement

============================================================
FINAL NOTE

After generating the code, validate it compiles and runs. If there are any build or runtime issues (including System.CommandLine API mismatches), iterate and fix until the verification commands succeed.
