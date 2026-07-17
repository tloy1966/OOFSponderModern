# Long Leave Feature Plan

## Goal

Let a user schedule one explicit, multi-day out-of-office period without changing their normal weekly work schedule. The selected interval must flow through the existing local preview, message-template, confirmation, and Microsoft Graph apply paths.

## Recommended MVP

Add a **Schedule source** choice with two modes:

1. **Weekly schedule** — existing behavior; calculates the next OOF interval from working hours.
2. **Long leave** — user enters an explicit leave start and return date/time.

Long leave is a local draft until the user selects **Apply to M365** and confirms the existing review prompt. Enabling it must not silently update Microsoft 365.

### Long-leave fields

- Start date and time.
- Return date and time.
- Optional leave label, such as “Vacation” or “Parental leave”, stored locally and shown only in the app's review text.
- A clear validation/status message.

Use the existing **Extended** message profile by default when long-leave mode is first selected. Keep the profile selector visible so the user can intentionally choose another profile.

## UX flow

1. In the Schedule tab, select **Long leave** under a new Schedule source control.
2. Enter start and return values. Defaults should be:
   - Start: current local date/time, rounded to the next 30 minutes.
   - Return: the next configured working-day start at least seven days later.
3. The status cards and sanitized preview immediately show the explicit interval and “Long leave” mode.
4. In Messages, generate or resolve an Extended-profile suggestion using the explicit interval. Existing variables (`{StartDate}`, `{StartTime}`, `{ReturnDate}`, `{ReturnTime}`, and `{Duration}`) continue to work.
5. Select **Apply to M365**. The confirmation identifies the schedule source, interval, profile, audience, and message presence/length without exposing message bodies.
6. Microsoft Graph receives the same scheduled automatic-reply payload shape used today.
7. Selecting **Weekly schedule** restores the normal calculated window without modifying the saved weekly hours.

## Data model

Add:

- `ScheduleSource` enum: `WeeklySchedule`, `LongLeave`.
- `LongLeaveSettings` model:
  - `DateTimeOffset Start`
  - `DateTimeOffset End`
  - `string Label`
- `AppState.LongLeave` for the saved draft.
- `UserPreferences.SelectedScheduleSource` for the active mode.

Keep `OofWindow` as the common downstream representation. Add a long-leave window factory or scheduler method that validates and returns an `OofWindow`; do not make the Graph client understand UI modes.

Increment settings schema from 2 to 3. Migration should create a disabled/default long-leave draft while retaining all weekly schedules, messages, templates, and preferences.

## Validation and safety

Block preview generation and Microsoft 365 apply when:

- End is not later than start.
- Either value cannot be parsed.
- The interval is zero or negative.

Warn, but do not block, when:

- Start is in the past; offer/reset to “start now” before apply.
- Duration is under three days, because weekly mode may be more appropriate.
- Duration exceeds one year, to catch likely input mistakes.

Before Graph apply, recalculate/validate against the current time. If a saved start has passed, require the user to confirm an adjusted start of now rather than sending an already-started timestamp accidentally.

All date/time conversion remains in local time and uses `DateTimeOffset`. Preserve the current privacy rule: labels may appear in local UI/activity, but message bodies never appear in diagnostics.

## Implementation slices

### 1. Models and persistence

- Add `ScheduleSource` and `LongLeaveSettings` models.
- Extend `AppState` and `UserPreferences`.
- Add schema-v3 migration and defaults in the file and in-memory settings services.
- Add JSON round-trip and migration regression checks.

### 2. Scheduling logic

- Extend `ISchedulerService` with an explicit-window method, or add a focused `ILongLeaveWindowService` if validation grows.
- Return a validated `OofWindow` with a reason such as “Explicit long-leave interval.”
- Keep existing weekly calculations unchanged.

### 3. View model

- Add schedule-source, start/end, label, validation, and long-leave visibility properties.
- Centralize active-window calculation so weekly and long-leave modes both update `_currentWindow` through one path.
- Mark generated suggestions stale whenever source or leave values change.
- Disable preview/apply commands when the active interval is invalid.
- On first switch to long leave, select the Extended template/apply profile and persist the choice.

### 4. WPF UI

- Add Schedule source controls at the top of the Schedule tab.
- Show an explicit-date card only in long-leave mode; retain the weekly editor and make its inactive state visually clear.
- Use WPF `DatePicker` controls plus time text boxes/30-minute adjustment buttons, following the existing schedule editor patterns.
- Surface inline validation and duration.
- Update onboarding and tooltips to explain that apply remains manual.

### 5. Preview and Graph apply

- Add schedule source and optional label to sanitized preview/review text.
- Reuse `MailboxSettingsPreview` and the existing Graph payload; only the `OofWindow` changes.
- Preserve the audience and selected-profile behavior.
- Ensure invalid intervals cannot reach `GraphMailboxSettingsClient.ApplyAsync()`.

### 6. Templates and documentation

- Reuse all current date/duration variables with no breaking changes.
- Add a default “Long leave” saved template only for new users. Do not insert it into existing customized template collections during migration.
- Update the README feature list, Schedule/Messages usage, local-data description, and regression-test coverage.

## Test plan

Add deterministic regression cases for:

- Valid future long-leave interval.
- Same-day start/end with positive duration.
- End equal to or before start.
- Start in the past and apply-time adjustment behavior.
- Interval crossing a daylight-saving transition with offsets preserved.
- Switching modes restores the weekly-calculated window.
- Long-leave changes invalidate generated suggestions.
- Existing template variables resolve from the explicit interval.
- Extended profile is selected only on the first intentional switch, not on every recalculation.
- Schema 2 settings migrate to schema 3 without losing data.
- Graph preview/payload uses the explicit dates, selected audience, and selected message profile.
- Message bodies remain absent from status, confirmation, and activity logs.

Run both the console regression harness and a Release build of the solution.

## Acceptance criteria

- A user can create and save a valid explicit leave interval without altering weekly hours.
- Switching schedule source updates all status cards, suggestions, preview, and confirmation consistently.
- The explicit interval is sent only after the current confirmation flow.
- Invalid or stale intervals cannot be applied.
- Existing weekly scheduling behavior and saved settings continue to work unchanged.
- Existing installations migrate without losing schedule, messages, templates, theme, or update preferences.
- The feature works across DST boundaries and after app restart.

## Non-goals for MVP

- Recurring leave periods or a leave calendar.
- Multiple saved/future leave entries.
- Automatic background activation or cancellation.
- Calendar-event creation, Teams status updates, delegation, or mailbox forwarding.
- Importing message bodies from Microsoft 365.

## Follow-up options

After the MVP is stable, consider multiple named leave drafts, backup-contact template variables, calendar conflict checks, and an explicit “disable automatic replies” action. Each should be a separate feature because it adds permissions, privacy, or lifecycle complexity.
