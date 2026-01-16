# ImageQueueProcessor

A .NET 10 console application that enqueues images to Azure Storage Queue and processes them by resizing and uploading to Azure Blob Storage.

## Features

- **Enqueue**: Read image files from a local folder and send them as messages to an Azure Storage Queue
- **Process**: Dequeue messages, resize images to 50% width (maintaining aspect ratio), and upload to Azure Blob Storage
- **Dry-run mode**: Preview what would happen without making any changes
- **Cross-platform**: Uses SixLabors.ImageSharp for image processing

## Configuration

### appsettings.json (checked in)

```json
{
  "QueueProcessing": {
    "QueueName": "images",
    "ResizedImagesContainer": "resizedimages"
  }
}
```

### appsettings.json.user (NOT checked in - contains secrets)

Create this file with your Azure Storage connection string:

```json
{
  "QueueProcessing": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"
  }
}
```

**Important**: The `appsettings.json.user` file contains secrets and should never be committed to source control.

## Usage

### Build

```bash
dotnet build
```

### Enqueue Images

Enqueue all images from a folder:

```bash
dotnet run -- enqueue --folder /path/to/images
```

With a specific pattern:

```bash
dotnet run -- enqueue --folder /path/to/images --pattern "*.png;*.jpg"
```

Dry-run (preview what would happen):

```bash
dotnet run -- enqueue --folder /path/to/images --dry-run
```

### Process Queue

Process all messages in the queue:

```bash
dotnet run -- process
```

Dry-run:

```bash
dotnet run -- process --dry-run
```

### Help

```bash
dotnet run -- --help
dotnet run -- enqueue --help
dotnet run -- process --help
```

## Behavior Details

### Enqueue Operation

1. Reads image files from the specified folder matching the pattern
2. Creates a JSON message containing the filename, Base64-encoded image data, and content type
3. Sends the message to the Azure Storage Queue
4. Deletes the local file after successful enqueue
5. If a file is too large for a queue message (~48KB limit), it logs an error and skips the file

### Process Operation

1. Dequeues messages from the Azure Storage Queue
2. Decodes the Base64 image data
3. Resizes the image to 50% of original width (maintaining aspect ratio)
4. Uploads to the configured blob container with filename suffix `-50` (e.g., `image-50.png`)
5. Deletes the queue message only after successful upload
6. Continues processing until the queue is empty

## Exit Codes

- `0`: Success - all operations completed without errors
- `1`: Configuration error (e.g., missing connection string)
- `2`: Processing error (e.g., failed to enqueue/upload one or more files)

## Supported Image Formats

- PNG
- JPEG
- GIF
- BMP
- WebP

## Requirements

- .NET 10.0 SDK
- Azure Storage Account with Queue and Blob access
