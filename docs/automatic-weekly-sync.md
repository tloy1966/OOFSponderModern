# Automatic Weekly Sync Phase 1

## Goal

Keep the Microsoft 365 automatic-reply interval aligned with the saved weekly schedule while OOFSponderModern is running, without requiring a manual apply every day.

## Behavior

- Automatic weekly sync is off by default and must be explicitly enabled under Configuration.
- Enabling it starts an immediate check and repeats every 10 minutes while the app remains open.
- Each check recalculates the active weekly `OofWindow`, silently acquires a cached token, and reads the current mailbox settings.
- A PATCH is sent only when status, audience, interval, or selected-profile messages differ.
- Starts that are already in the past are treated as equivalent when both local and remote intervals are active and their end times match. This prevents a new PATCH every 10 minutes after work begins.
- Long Leave immediately cancels and pauses weekly synchronization. Returning to Weekly schedule starts an immediate check.
- Closing the app cancels the timer. `Start with Windows` can be used to launch the app at sign-in, but the app must remain open.

## Authentication and safety

- Automatic sync never opens an interactive sign-in prompt.
- If no cached account or silent token is available, status instructs the user to complete one manual Apply to M365.
- Manual apply keeps its existing review and confirmation flow.
- Diagnostics and activity omit message bodies.
- Graph failures are retained as retry status; the next 10-minute check continues normally.

## Non-goals

- System tray behavior after closing the window.
- Windows Service or Task Scheduler integration.
- Sync while the application is not running.
- Precision scheduling exactly at each work boundary.
- Automatic Long Leave apply.

## Validation

- Enabled weekly mode invokes compare-before-apply and reports whether a PATCH occurred.
- Disabled mode performs no Graph call.
- Long Leave performs no automatic Graph call.
- Active intervals with an already-past start and matching end do not repeat PATCH.
- Changed end times are detected.
- The preference survives settings reload.
- Closing the window cancels automatic synchronization.