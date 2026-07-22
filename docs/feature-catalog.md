# Feature Catalog

This catalog is the implementation map for current behavior. Use it before changing a feature so its model, service, UI, persistence, safety, and tests move together.

## Weekly schedule

**Behavior:** Edit per-day start/end times, mark off-work days, optionally shift linked start/end values, and calculate the next OOF interval. Schedule edits persist across app restarts.

**Owners:**

- Models: `ScheduleDay`, `AppState.WeeklySchedule`, `UserPreferences.IsLinkedTimeAdjustmentEnabled`.
- Logic: `SchedulerService`, `ScheduleDayViewModel`.
- Coordination: `MainViewModel.RecalculateWindow` and weekly schedule save handling.
- UI: Schedule tab in `MainWindow.xaml`.
- Persistence: `FileSettingsService` and `InMemorySettingsService`.

**Minimum regression coverage:** before/during/after work, off-work and all-off-work days, linked shifts, DST, direct time edits, immediate close, and disk reload.

## Long leave

**Behavior:** Select an explicit start and return interval without changing weekly hours. Invalid intervals cannot be previewed or applied; past starts require adjustment confirmation.

**Owners:** `LongLeaveSettings`, `ScheduleSource`, `SchedulerService.CalculateLongLeaveWindow`, long-leave properties in `MainViewModel`, and the Schedule tab.

**Persistence:** schema 3 state plus `UserPreferences.SelectedScheduleSource`.

**Minimum regression coverage:** valid/invalid intervals, DST offsets, stale suggestion invalidation, first-switch Extended defaults, mode switching, migration, and restart. See [long-leave-feature-plan.md](long-leave-feature-plan.md) for the original acceptance criteria.

## Message profiles and audience

**Behavior:** Maintain Primary and Extended internal/external replies. Apply one selected profile with audience `None`, `Contacts Only`, or `All External`.

**Owners:** `MessageProfile`, `AudienceScope`, `TemplateTargetProfile`, message properties in `MainViewModel`, and the Messages tab.

**Safety:** internal replies remain enabled; `None` sends an empty external body; message bodies never enter diagnostics or activity.

**Minimum regression coverage:** selected profile, audience mapping, empty external message for `None`, persistence, and sanitized preview text.

## Suggestions and saved templates

**Behavior:** Generate local rule-based suggestions or resolve named templates against the active OOF window. Applying a suggestion only copies text into a local profile.

**Owners:** `LocalOofTemplateGenerator`, `MessageTemplateRenderer`, `MessageTemplate`, and suggestion/template commands in `MainViewModel`.

**Persistence:** named templates and profile messages persist; generated suggestions are temporary.

**Language:** generated dates and template date/time variables are always English (`en-US`) regardless of OS culture.

**Minimum regression coverage:** variable replacement, unknown variables, duration, active weekly/long-leave window, `zh-TW` current culture, persistence, and stale-result invalidation.

## Microsoft 365 apply and readback

**Behavior:** Authenticate with MSAL, display sanitized current mailbox status, preview a local payload, manually PATCH after confirmation, and optionally keep the weekly interval current every 10 minutes while the app is open.

**Owners:** `GraphMailboxSettingsClient`, `IMailboxSettingsClient`, `MailboxSettingsPreview`, `CurrentMailboxSettingsSummary`, and apply/readback commands in `MainViewModel`.

**Permissions:** delegated `user.read` and `MailboxSettings.ReadWrite`.

**Safety:** automatic sync is opt-in, uses silent authentication, compares remote state before PATCH, pauses during Long leave, and stops when the app exits. No remote message import, no message bodies in diagnostics, validated local-time interval, and selected profile/audience only.

**Minimum regression coverage:** payload schedule/profile/audience mapping, compare-before-apply behavior, stable active intervals, Long Leave pause, invalid-window blocking, silent-auth requirements, cancellation, sanitization, and Graph error status.

## Local settings and startup

**Behavior:** Persist application state under AppData, restore window dimensions, optionally start with Windows, and enforce a single running instance.

**Owners:** `FileSettingsService`, `AppState`, `UserPreferences`, `WindowsStartupService`, `App.xaml.cs`, and window lifecycle code in `MainWindow.xaml.cs`.

**Minimum regression coverage:** defaults, JSON round trip, schema migration, customized data preservation, final close save, and startup preference synchronization.

## Appearance and onboarding

**Behavior:** Light/dark mode, four palettes, first-run guidance, and centered window startup with restored dimensions.

**Owners:** `ThemePalette`, appearance/onboarding properties in `MainViewModel`, and dynamic resources in `App.xaml` and `MainWindow.xaml`.

**Minimum validation:** persistence plus manual visual checks for light/dark themes, each palette, text contrast, clipping, and minimum window size.

## Updates and releases

**Behavior:** Check the latest stable public GitHub Release, cache results for 24 hours, show release notes, open HTTPS release pages, and allow skipping a version. Never download or install automatically.

**Owners:** `GitHubReleaseUpdateService`, `ReleaseInformation`, `UpdateState`, update commands in `MainViewModel`, `.github/workflows/release.yml`, and `SupportingFiles/publish-modern-github.ps1`.

**Minimum regression coverage:** semantic-version ordering, prerelease handling, cache/skip behavior, cancellation, and invalid release data. Release validation must run tests, build, and publish before uploading an artifact.

## Diagnostics and activity

**Behavior:** Show schedule/auth/sync status, sanitized payload previews, current mailbox metadata, and recent local activity.

**Owners:** `SyncState`, preview/activity builders in `MainViewModel`, and the Sync / Diagnostics tab.

**Safety:** never include message text, tokens, or raw mailbox responses. Presence and character counts are acceptable.

## Adding a feature

Use the engineering workflow and Definition of Done in [CONTRIBUTING.md](../CONTRIBUTING.md). Update this catalog whenever a feature adds a persisted field, service boundary, Graph permission/payload field, user command, or new validation responsibility.