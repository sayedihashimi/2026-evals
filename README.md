# Azure Samples Repository

This repository contains several Azure sample applications, each on its own branch. The samples demonstrate different Azure services including Azure Functions, Azure Blob Storage, Azure Queue Storage, and Azure Communication Services.

## Repository Structure

Each sample is maintained on a separate branch and can be checked out independently:

| Branch | PR | Sample | Description |
|--------|--------|--------|-------------|
| `azfunc-gh-pr-gatekeeper` | [diff](https://github.com/sayedihashimi/2026-evals/pull/1)  | GitHub PR Gatekeeper | Azure Function webhook that validates PR titles, branch names, and descriptions |
| `azfunc-healthcheck` | [diff](https://github.com/sayedihashimi/2026-evals/pull/2) | Health Check Function | Azure Function that monitors website health and sends email alerts |
| `azstorage-log-file-uploader` | [diff](https://github.com/sayedihashimi/2026-evals/pull/3) | Log File Uploader | Console app that uploads log files to Azure Blob Storage |
| `azstorage-queue-image` | [diff](https://github.com/sayedihashimi/2026-evals/pull/4) | Image Queue Processor | Console app that queues and processes images with resizing |

---

## Prompt files

| User prompt file | Detailed prompt file that I used to start with|
|--------|--------|
| [create-azfunction-github-pr-gatekeeper-user.md](tools/prompts/user%20prompts/create-azfunction-github-pr-gatekeeper-user.md) | [create-azfunction-github-pr-gatekeeper.md](tools/prompts/detailed%20prompts/create-azfunction-github-pr-gatekeeper.md) |
| [create-azfunction-health-check-user.md](tools/prompts/user%20prompts/create-azfunction-health-check-user.md) | [create-azfunction-healthcheck.md](tools/prompts/detailed%20prompts/create-azfunction-healthcheck.md) |
| [create-storage-logs-app-user.md](tools/prompts/user%20prompts/create-storage-logs-app-user.md) | [create-storage-logs-app.md](tools/prompts/detailed%20prompts/create-storage-logs-app.md) |
| [create-storage-queue-app-user.md](tools/prompts/user%20prompts/create-storage-queue-app-user.md) | [create-storage-queue-app.md](tools/prompts/detailed%20prompts/create-storage-queue-app.md) |

## Sample 1: GitHub PR Gatekeeper (`azfunc-gh-pr-gatekeeper`)

An Azure Function that acts as a GitHub webhook to validate Pull Requests against organizational standards.

### Project Location
```
AzureFunctions/GitHubPrWebhook/FunctionApp1/
```

### Features
- Validates PR title prefix (bug|feature|perf|docs|refactor|test|chore)
- Validates branch naming convention (`{username}/{description}`)
- Ensures PR description is at least 100 characters
- Posts automated comments on PRs with pass/fail status

### Azure Resources Required

1. **Azure Function App** (optional for local development)
   - Runtime: .NET 10.0 Isolated
   - Can run locally using Azure Functions Core Tools

2. **GitHub Repository** with webhook configured
   - Configure a GitHub repository to have a webhook which sends PR notifications.
   - Use dev tunnels in VS with a `persistent` tunnel.
   - In GitHub repo settings the Payload URL should look like `https://<VALUE>.devtunnels.ms/api/github/webhooks/pr-gatekeeper`.
   - Make sure the app is running locally with dev tunnels when you submit a PR to the configured repo.
   - You need to create a token in GitHub that has access to the repo specified. That token is the value for the `GitHub:PatToken` config.
   - When creating the webhook, create a secret and paste that for the value of `GitHub:WebhookSecret`.

### Configuration

Create or modify `local.settings.json` in the `FunctionApp1` folder:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "GitHub:PatToken": "<your-github-personal-access-token>",
    "GitHub:WebhookSecret": "<your-webhook-secret>"
  }
}
```

### Required Secrets

| Setting | Description | How to Obtain |
|---------|-------------|---------------|
| `GitHub:PatToken` | GitHub Personal Access Token | GitHub → Settings → Developer settings → Personal access tokens → Generate new token. Requires `repo` scope for commenting on PRs. |
| `GitHub:WebhookSecret` | Webhook secret for signature validation | Create a secure random string (e.g., `openssl rand -base64 32`) and use the same value when configuring the GitHub webhook. |

### Running Locally

1. Open solution in Visual Studio
2. Select, or create, a persistent dev tunnel to be used when running the app
3. Configure `local.settings.json` with your secrets
4. Configure a GitHub repo with a webhook
5. Run the app
6. Submit a PR to the configured repo

---

## Sample 2: Health Check Function (`azfunc-healthcheck`)

An Azure Function that periodically checks website availability and sends email alerts using Azure Communication Services.

### Project Location
```
AzureFunctions/HealthCheck/FunctionApp1/
```

### Features
- Timer-triggered health checks (configurable schedule)
- Stores health check results in SQLite database
- Sends email alerts on failures via Azure Communication Services
- Configurable timeout and target URL

### Azure Resources Required

1. **Azure Function App** (optional for local development)
   - Runtime: .NET 10.0 Isolated

2. **Azure Communication Services**
   - Create an Azure Communication Services resource
   - Set up an Email Communication Service
   - Configure a verified sender domain

### Configuration

Create or modify `local.settings.json` in the `FunctionApp1` folder:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    
    "HealthCheck:Schedule": "0 */1 * * * *",
    "HealthCheck:TargetUrl": "https://your-site-to-monitor.com/",
    "HealthCheck:TimeoutSeconds": "5",
    
    "Email:ConnectionString": "<your-acs-connection-string>",
    "Email:SenderAddress": "<your-verified-sender-email>",
    "Email:RecipientAddress": "<email-to-receive-alerts>"
  },
  "ConnectionStrings": {
    "HealthChecks": "Data Source=healthchecks.db"
  }
}
```

### Required Secrets

| Setting | Description | How to Obtain |
|---------|-------------|---------------|
| `Email:ConnectionString` | Azure Communication Services connection string | Azure Portal → Communication Services resource → Keys → Connection string |
| `Email:SenderAddress` | Verified sender email address | Azure Portal → Communication Services → Email → Domains → Add a verified sender |
| `Email:RecipientAddress` | Email address to receive alerts | Your personal or team email address |

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `HealthCheck:Schedule` | `0 */1 * * * *` | CRON expression (default: every minute) |
| `HealthCheck:TargetUrl` | `https://aspire.dev/` | URL to monitor |
| `HealthCheck:TimeoutSeconds` | `5` | HTTP request timeout |

### Setting Up Azure Communication Services

1. **Create Communication Services Resource**
   - Go to Azure Portal → Create a resource → Communication Services
   - Note the connection string from Keys blade

2. **Set Up Email Domain**
   - In your Communication Services resource, go to Email → Domains
   - Add a domain (Azure-managed or custom)
   - Verify the domain if using custom

3. **Configure Sender Address**
   - Under Domains, select your domain
   - Add a sender address (e.g., `DoNotReply@<your-domain>.azurecomm.net`)

### Running Locally

1. Open solution in Visual Studio
2. Fill in the secrets specified above
3. Run the app

---

## Sample 3: Log File Uploader (`azstorage-log-file-uploader`)

A .NET 10 console application that uploads log files to Azure Blob Storage.

### Project Location
```
AzureStorage/LogFileUploader/LogFileUploader/
```

### Features
- Scans directory for log files
- Uploads to Azure Blob Storage with retry logic (Polly)
- Deletes local files after successful upload
- Supports dry-run mode
- Cross-platform compatible

### Azure Resources Required

1. **Azure Storage Account**
   - Create a general-purpose v2 storage account
   - A blob container will be created automatically (default: `logs`)

### Configuration

The app uses two configuration files:

**appsettings.json** (checked in - no secrets):
```json
{
  "BlobUpload": {
    "ContainerName": "logs",
    "BlobPrefix": "ingest/",
    "ContentType": "text/plain",
    "Overwrite": false,
    "MaxFileSizeBytes": 104857600,
    "MinFileSizeBytes": 1,
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

**appsettings.json.user** (NOT checked in - create this file with secrets):
```json
{
  "BlobUpload": {
    "ConnectionString": "<your-storage-connection-string>"
  }
}
```

### Required Secrets

| Setting | Description | How to Obtain |
|---------|-------------|---------------|
| `BlobUpload:ConnectionString` | Azure Storage connection string | Azure Portal → Storage Account → Access keys → Connection string |

### Setting Up Azure Storage Account

1. **Create Storage Account**
   - Go to Azure Portal → Create a resource → Storage account
   - Choose Standard performance, StorageV2 (general purpose v2)
   - Note: the `logs` container will be created automatically on first run

2. **Get Connection String**
   - Go to your Storage Account → Access keys
   - Copy the Connection string (key1 or key2)

### Running the Application

```powershell
cd AzureStorage/LogFileUploader/LogFileUploader

# Build the project
dotnet build

# View help
dotnet run -- --help

# Upload log files from default directory (%AppData%/MyStorageApp/logs/)
dotnet run

# Upload from specific directory
dotnet run -- --directory "C:\logs" --pattern "*.log"

# Dry-run (preview without uploading)
dotnet run -- --directory "C:\logs" --dry-run

# Verbose logging
dotnet run -- --directory "C:\logs" --verbose
```

Note: you can create fake log files using the PowerShell script at `/tools/New-FakeLogs.ps1`. You can specify the `-OutputDirectory` for the logs.

### Command Line Options

| Option | Default | Description |
|--------|---------|-------------|
| `--directory`, `-d` | `%AppData%/MyStorageApp/logs/` | Directory containing log files |
| `--pattern`, `-p` | `*.log` | File pattern to match |
| `--delete` | `true` | Delete files after successful upload |
| `--dry-run` | `false` | Simulate without uploading/deleting |
| `--verbose`, `-v` | `false` | Enable debug logging |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Configuration or argument error |
| 2 | One or more uploads failed |

---

## Sample 4: Image Queue Processor (`azstorage-queue-image`)

A .NET 10 console application that enqueues images to Azure Storage Queue and processes them by resizing and uploading to Azure Blob Storage.

### Project Location
```
AzureStorage/ImageQueue/ImageQueueProcessor/
```

### Features
- **Enqueue**: Read images from local folder, send to Azure Queue, delete local files
- **Process**: Dequeue messages, resize images to 50% width, upload to Blob Storage
- Supports dry-run mode
- Uses SixLabors.ImageSharp for cross-platform image processing

### Azure Resources Required

1. **Azure Storage Account**
   - Create a general-purpose v2 storage account
   - Queue and blob containers will be created automatically

### Configuration

The app uses two configuration files:

**appsettings.json** (checked in - no secrets):
```json
{
  "QueueProcessing": {
    "QueueName": "images",
    "SourceImagesContainer": "sourceimages",
    "ResizedImagesContainer": "resizedimages",
    "MaxFileSizeBytes": 5242880
  }
}
```

**appsettings.json.user** (NOT checked in - create this file with secrets):
```json
{
  "QueueProcessing": {
    "ConnectionString": "<your-storage-connection-string>"
  }
}
```

### Required Secrets

| Setting | Description | How to Obtain |
|---------|-------------|---------------|
| `QueueProcessing:ConnectionString` | Azure Storage connection string | Azure Portal → Storage Account → Access keys → Connection string |

### Setting Up Azure Storage Account

1. **Create Storage Account**
   - Go to Azure Portal → Create a resource → Storage account
   - Choose Standard performance, StorageV2 (general purpose v2)
   - Note: queue and containers are created automatically on first run

2. **Get Connection String**
   - Go to your Storage Account → Access keys
   - Copy the Connection string (key1 or key2)

### Running the Application

```powershell
cd AzureStorage/ImageQueue/ImageQueueProcessor

# Build the project
dotnet build

# View help
dotnet run -- --help
dotnet run -- enqueue --help
dotnet run -- process --help

# Enqueue images from default directory (%AppData%/MyStorageApp/sourceimages)
dotnet run -- enqueue

# Enqueue from specific folder
dotnet run -- enqueue --folder "C:\Images" --pattern "*.png;*.jpg"

# Dry-run enqueue (preview without changes)
dotnet run -- enqueue --folder "C:\Images" --dry-run

# Process queue (resize and upload)
dotnet run -- process

# Dry-run process (preview without changes)
dotnet run -- process --dry-run
```

### Command Line Options

**Enqueue Command:**
| Option | Default | Description |
|--------|---------|-------------|
| `--folder` | `%AppData%/MyStorageApp/sourceimages` | Folder containing images |
| `--pattern` | `*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp` | File patterns to match |
| `--dry-run` | `false` | Simulate without changes |

**Process Command:**
| Option | Default | Description |
|--------|---------|-------------|
| `--dry-run` | `false` | Simulate without changes |

### Behavior Notes

- Images are Base64-encoded and sent as queue messages
- Files larger than ~48KB cannot be queued (logged as error, file not deleted)
- Resized images are saved with `-50` suffix (e.g., `photo-50.jpg`)
- Original files are deleted only after successful enqueue
- Queue messages are deleted only after successful blob upload

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Configuration error |
| 2 | Processing error (one or more files failed) |

### Supported Image Formats

PNG, JPEG, GIF, BMP, WebP

---

## Prerequisites

All samples require:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Subscription](https://azure.microsoft.com/free/)

For Azure Functions samples:
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) (for local storage emulation)

---

## Quick Start Summary

| Sample | Config File | Key Secret(s) |
|--------|-------------|---------------|
| GitHub PR Gatekeeper | `local.settings.json` | `GitHub:PatToken`, `GitHub:WebhookSecret` |
| Health Check | `local.settings.json` | `Email:ConnectionString`, `Email:SenderAddress` |
| Log File Uploader | `appsettings.json.user` | `BlobUpload:ConnectionString` |
| Image Queue Processor | `appsettings.json.user` | `QueueProcessing:ConnectionString` |

---

## Security Notes

⚠️ **Never commit secrets to source control!**

- `local.settings.json` and `appsettings.json.user` files are in `.gitignore`
- Always use environment variables or Azure Key Vault for production deployments
- Rotate secrets periodically
- Use minimal-privilege access policies when creating service connections

---

## License

See [LICENSE](LICENSE) for details.
