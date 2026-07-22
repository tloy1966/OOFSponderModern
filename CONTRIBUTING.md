# Contributing to OOFSponderModern

This document is the engineering standard for changes to OOFSponderModern. Read it together with [docs/feature-catalog.md](docs/feature-catalog.md), which maps current features to their owning code and required regression coverage.

## Supported environment

- Windows and PowerShell.
- .NET 10 SDK.
- WPF application in `OOFSponderModern/`.
- Console regression harness in `OOFSponderModern.Tests/`.

## Architecture boundaries

Keep dependencies flowing in this direction:

```text
MainWindow.xaml
    -> ViewModels
        -> service interfaces
            -> service implementations
                -> Models
```

- **Models** contain persisted state and transport values. They must not depend on WPF.
- **Services** own scheduling, persistence, template rendering, Microsoft Graph, startup, and release-check behavior.
- **ViewModels** coordinate UI state and services. Keep Graph JSON, file I/O, and scheduling algorithms out of view models.
- **Views** contain layout, binding, and minimal window lifecycle code. Do not put business decisions in code-behind.
- Use an existing interface when substituting test behavior or an external dependency. Add a new abstraction only when it creates a real ownership boundary.

## C# and WPF conventions

- Nullable reference types and implicit usings remain enabled.
- Use four spaces, file-scoped namespaces, PascalCase public members, and `_camelCase` private fields.
- Prefer immutable `record` types for result/request values and simple classes for persisted mutable state.
- Use `DateTimeOffset` for OOF intervals. Preserve offsets across daylight-saving transitions.
- Pass an explicit `CultureInfo` for user-facing generated content that must have a fixed language. Do not let OS culture change English suggestions or Graph wire values.
- Use `async` service APIs for I/O. Do not block the UI thread with network calls.
- Commands that can become invalid must expose and refresh `CanExecute` state.
- Bind editable text with an intentional `UpdateSourceTrigger`. A value that must survive immediate window close must be committed before the final settings save.
- Keep comments rare and focused on non-obvious constraints.

## Persistence rules

User settings live in `%APPDATA%\OOFSponderModern\usersettings.json`.

- Every user-editable setting advertised as persistent must update `AppState` and call the existing settings save path.
- Use debounced saving for rapid text edits. Use an immediate or final save for discrete changes and window close.
- New persisted fields require defaults in `InMemorySettingsService`, null/default repair in `FileSettingsService`, and a regression test that reloads from disk.
- For a breaking state-shape change, increment `AppState.SchemaVersion` and add a migration that preserves all unrelated user data.
- Never reset an existing schedule, message, template, or preference merely because a new default was introduced.

## Microsoft 365 and privacy rules

- Manual apply must remain reviewed and confirmed. Automatic apply must be explicitly enabled, use silent authentication, compare remote state before PATCH, and expose its current status.
- Keep schedule calculation outside `GraphMailboxSettingsClient`; Graph receives a validated `MailboxSettingsPreview`.
- Continue using delegated `user.read` and `MailboxSettings.ReadWrite` scopes unless a feature explicitly justifies and documents another permission.
- Do not write message bodies, access tokens, or mailbox response content to diagnostics or recent activity.
- Readback may report message presence and character count, but must not import or display remote message bodies.
- `AudienceScope.None` must send an empty external reply.
- Graph date/time values use the local Windows time-zone ID and the existing nested `automaticRepliesSetting` payload.

## Feature workflow

For each feature or bug fix:

1. **Define behavior.** Write acceptance criteria, affected user data, privacy/permission impact, and explicit non-goals. Use a plan under `docs/` for cross-cutting work.
2. **Find the owner.** Start with [docs/feature-catalog.md](docs/feature-catalog.md), then trace from the deciding service or model rather than only the XAML control.
3. **Implement one vertical slice.** Update model/default/migration, service, view model, and UI only where the feature requires them.
4. **Protect saved data.** Verify both a new installation and an existing settings file. Add restart coverage for persistent behavior.
5. **Protect safety boundaries.** Validate intervals before Graph calls, preserve confirmation, and keep message bodies out of logs.
6. **Add regression coverage.** Tests must reproduce the failure or prove the acceptance criterion, including culture, time zone, DST, and restart cases when relevant.
7. **Update documentation.** Update the README for user-visible behavior and the feature catalog when ownership, persistence, permissions, or commands change.
8. **Validate and review.** Run the required commands below and inspect the focused diff before committing.

## Required validation

Run both commands for every runtime change:

```powershell
dotnet run --project .\OOFSponderModern.Tests\OOFSponderModern.Tests.csproj --configuration Release
dotnet build .\OOFSponder.sln --configuration Release
```

For UI changes, also launch the app and manually check the affected workflow at a normal desktop size and at the minimum supported window size:

```powershell
dotnet run --project .\OOFSponderModern\OOFSponderModern.csproj --configuration Release
```

The console harness is intentionally lightweight. Add deterministic methods to `OOFSponderModern.Tests/Program.cs`; avoid current-clock dependencies when a fixed date can express the behavior.

## Definition of done

A change is complete when:

- Acceptance criteria and error states work end to end.
- Existing settings migrate without unrelated data loss.
- Restart behavior is tested for persistent fields.
- Preview and Microsoft 365 apply use the same validated active window and selected profile.
- Diagnostics contain no message bodies or credentials.
- Regression tests and the Release build pass.
- README and feature catalog remain accurate.
- The commit contains only related files and uses a concise imperative message.

## Release process

Do not create a release until the regression harness, solution build, and publish script succeed. Releases use semantic versions such as `v1.0.0` or `v1.0.0-beta.1`; see the README for tag and manual workflow instructions.