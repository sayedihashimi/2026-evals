You are generating a PowerShell Core (pwsh) script named `New-FakeLogs.ps1` that creates fake log files.

Requirements
- PowerShell Core compatible (7+). No Windows-only cmdlets.
- Provide parameters:
  - `-Count` (int) = number of log files to create. Default: 10.
  - `-OutputDirectory` (string) = directory to create the files in. Default: current working directory (PWD).
- Create the output directory if it doesn’t exist.

File naming
- Each file name MUST be in this format:
  `yyyy.MM.dd.HH.mm.ss.ffff-{randomstring}.log`
  Example: `2025.01.15.05.20.32.1245-abc12.log`
- The timestamp’s DATE portion (yyyy.MM.dd) must be TODAY (local time).
- The time portion must be randomized per file:
  - Hour: 00-23
  - Minute: 00-59
  - Second: 00-59
  - Milliseconds: 0000-9999 (4 digits)
- `randomstring` must be exactly 5 characters, lowercase letters + digits only.

Log content
- Write some realistic fake log lines (at least 20 lines per file).
- Each line should start with an ISO 8601 timestamp and a log level (INFO/WARN/ERROR/DEBUG).
- Include some variety: different messages, random IDs, maybe a fake URL/path, etc.

Implementation details
- Use a `param(...)` block and strict mode.
- Validate `Count` is >= 1.
- Use `Join-Path` for paths.
- Ensure filenames are unique even if timestamps collide (the random suffix helps, but also handle collisions by regenerating).
- At the end, output a summary: directory, number of files created, and a few sample filenames.

Deliverable
- Output only the full contents of `New-FakeLogs.ps1` in a single PowerShell code block.
