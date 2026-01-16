You are working inside an EXISTING Azure Functions project in C# using the .NET isolated worker model (Azure Functions v4). Do NOT create a new project or change the project type. Add a new HTTP-triggered function to the current project.

Goal
- GitHub will POST webhook events to this function.
- The function validates the webhook signature using X-Hub-Signature-256 (HMAC SHA-256) with a secret stored in configuration.
- For supported PR events, it evaluates rules and posts a comment to the PR indicating PASS/FAIL and any actions the author must take.

Supported webhook events
- Only handle GitHub event "pull_request" with actions: opened, edited, reopened.
- For any other event/action: return 202 Accepted with a short JSON stating it was ignored.

Configuration (local dev)
- Use user secrets / appsettings.json.user (do NOT hardcode).
- Required settings:
  - GitHub:WebhookSecret  (string) shared webhook secret used for signature validation
  - GitHub:PatToken       (string) GitHub PAT used to call GitHub REST API
- Read these via IConfiguration.

HTTP endpoint
- Route: /api/github/webhooks/pr-gatekeeper
- AuthorizationLevel: Anonymous (GitHub can call it)
- Accept: application/json
- Return JSON results.

Validation rules (apply to the PR payload)
1) Title must start with one of these prefixes followed immediately by ':' (case-insensitive):
   bug|feature|perf|docs|refactor|test|chore
   Examples: "test: add coverage", "BUG: fix crash" (case-insensitive allowed)
2) Source branch name must match: {dev-username}/{description}
   - dev-username segment may contain letters, numbers, '.', '_' or '-'
   - Must contain exactly one '/' separating username from the rest
   - Do NOT validate the username; only validate the format
   - Description part may contain any characters, but must be at least 1 character
   - Use pull_request.head.ref for the branch name
3) PR description (pull_request.body) must be at least 100 characters, counting whitespace. Treat null as length 0.

Behavior
- Always post a NEW comment on the PR (do not update existing comments).
- The comment should clearly show PASS or FAIL.
- If FAIL, list each failed rule with a short “how to fix” bullet.
- If PASS, say all checks passed.
- Include a small marker line at the bottom like: "<!-- pr-gatekeeper -->" so it can be searched later (even though we’re not updating).

GitHub API call
- Use GitHub REST API to create an issue comment on the PR:
  POST /repos/{owner}/{repo}/issues/{issue_number}/comments
- Extract owner/repo from pull_request.base.repo.owner.login and pull_request.base.repo.name.
- issue_number comes from pull_request.number.
- Authenticate with the PAT via Authorization header (use "Bearer" token scheme).
- Set a User-Agent header.
- Use HttpClient via IHttpClientFactory and DI.

Webhook signature validation (best practice)
- Read headers:
  - X-Hub-Signature-256 (format: "sha256=...hex...")
  - X-GitHub-Event
  - X-GitHub-Delivery (optional, log it)
- Compute HMACSHA256 over the raw request body bytes using the WebhookSecret, compare to header signature in constant time.
- If signature missing/invalid: return 401 Unauthorized with JSON error and do NOT call GitHub.
- Important: read the raw body as bytes once and reuse it both for signature validation and JSON deserialization.

Implementation details
- Add a new function class file: GitHubPrGatekeeperFunction.cs
- Create strongly typed minimal models for only the parts of the pull_request payload we need (do NOT generate massive full schema).
- Use System.Text.Json with camelCase.
- Add structured logging including delivery id, repo, PR number, action, pass/fail.
- Return 200 OK with JSON { handled: true, passed: bool, failures: [...] } for processed events.

DI / wiring
- If the project already has HttpClient registration, reuse it. Otherwise:
  - Update the existing Program.cs to register an HttpClient named "GitHub".
- Do not break existing registrations; keep changes minimal and additive.

Local settings note
- Add an example snippet (comment only) showing appsettings.json.user keys:
  GitHub:WebhookSecret and GitHub:PatToken
- Do NOT commit secrets.

Edge cases
- pull_request.body can be null.
- Title/branch missing should be treated as failures.
- For ignored events/actions return 202.

Generate compile-ready code compatible with the existing project, with correct using statements and minimal, safe modifications.
