I have an existing .NET solution and want to add a new .NET console app to it.

The app should:
- Scan a directory for log files
- Upload each file to Azure Blob Storage
- Delete the local file after a successful upload
- Log progress and errors
- Support a dry-run mode

Use C# (.NET 10), async code, and Azure.Storage.Blobs.
The directory, file pattern, and Azure connection string should be configurable.
Basic command-line arguments are fine.
