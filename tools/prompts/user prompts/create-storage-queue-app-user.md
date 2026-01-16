I have an existing .NET solution and want to add a new .NET 10 console app to it.

The app should work with Azure Storage and support two basic actions:

1) Enqueue images:
- Read image files from a local folder
- Send each image to an Azure Storage Queue
- Delete the local image after it’s successfully queued

2) Process images:
- Read messages from the queue
- Resize each image to about 50% of its original size
- Upload the resized image to Azure Blob Storage
- Remove the queue message after a successful upload

The app should support a --dry-run option that only logs what would happen.

Use C#, async code, Azure Storage SDKs, and a simple command-line interface.
Configuration (connection string, queue name, container name) should come from appsettings files.
