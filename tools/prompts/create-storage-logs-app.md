# Prompt: Add .NET 10 console app to upload log files to Azure Blob Storage

You are modifying an EXISTING repository that already contains a .NET solution (.sln). Create a NEW .NET 10 console app project and add it to the existing solution.

## Goal
Create a console app that:
1) Takes a directory path that contains log files.
2) Uploads each log file to Azure Blob Storage (container) configured via `appsettings.json`.
3) Deletes each local file ONLY after a successful upload.
4) Processes all files and then exits.
5) If any error occurs (including a failed upload or delete), log it and exit with a non-zero exit code.

---

## Constraints / Requirements
- Target framework: **net10.0**.
- Language: C# latest.
- Use `Microsoft.Extensions.Hosting` (generic host) for:
  - Configuration (appsettings + environment variables + user file)
  - Logging
  - Dependency injection
- Use `Azure.Storage.Blobs` SDK (do NOT shell out to az cli).
- Use async I/O end-to-end.
- Cross-platform.

---

## New project
- Project name: `LogBlobUploader` (or similar if name conflicts).
- Place under `src/LogBlobUploader/` if the repo uses `src/`, otherwise follow existing conventions.
- Add to the existing `.sln`.
- Ensure `dotnet build` succeeds.

---

## Command line interface
- Usage:
  - `LogBlobUploader [--directory <path>]`
- Parameters:
  - `--directory` (optional)
    - If NOT provided, default to:
      - `%AppData%/MyStorageApp/logs/`
    - Resolve `%AppData%` using:
      - `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
    - Combine paths safely using `Path.Combine`.
- Validate:
  - Directory exists
  - Directory is readable

Optional flags (OK to include):
- `--pattern` (default `*.log`)
- `--dry-run`
- `--delete` (default `true`)

If optional flags are added, document them in `--help`.

---

## Configuration
Load configuration from:
- `appsettings.json` (checked in)
- `appsettings.json.user` (NOT checked in; secrets only)

### appsettings.json (checked in)
Create a stub with safe defaults and NO secrets:

```json
{
  "BlobUpload": {
    "ContainerName": "logs",
    "BlobPrefix": "ingest/",
    "ContentType": "text/plain",
    "Overwrite": false
  }
}
