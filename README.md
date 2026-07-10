## OOFSponder

OOFSponder schedules Microsoft 365 automatic replies from your normal working hours.

The app is now centered on **OOFSponderModern**, a Windows WPF app distributed from GitHub Releases.

## Install

Download the latest `OOFSponderModern-win-x64.zip` from GitHub Releases, extract it, and run `OOFSponderModern.exe`.

> OOFSponderModern is distributed via GitHub Releases, not ClickOnce.

## Build from source

Requirements:

- Windows
- .NET 10 SDK

Build:

```powershell
dotnet build .\OOFSponder.sln --configuration Debug
```

Run the modern app:

```powershell
Start-Process .\OOFSponderModern\bin\Debug\net10.0-windows\OOFSponderModern.exe
```

Run tests:

```powershell
dotnet run --project .\OOFSponderModern.Tests\OOFSponderModern.Tests.csproj --configuration Debug
```

Create a GitHub-release-ready archive locally:

```powershell
.\SupportingFiles\publish-modern-github.ps1 -Configuration Release -Runtime win-x64
```

The zip is written to `artifacts\OOFSponderModern-win-x64.zip`.

## Microsoft 365 permissions

OOFSponderModern uses Microsoft authentication and Microsoft Graph with these delegated scopes:

- `user.read`
- `MailboxSettings.ReadWrite`

Clicking `Apply to M365` updates your mailbox automatic replies. The app shows a confirmation prompt before sending the Graph update. Message bodies are not written to diagnostics.

## Usage

Use the `Schedule` tab to set normal working hours. Mark a day as `Off work` when automatic replies should stay active through the next working start.

Use `Link start/end time` when you want changing either workday start or end to shift the other time by the same amount.

Use the `Messages` tab to edit the Primary and Extended internal/external reply profiles.

Use `Profile to apply` in the `Schedule` tab to choose whether `Apply to M365` sends the Primary or Extended profile to Microsoft 365.

Use `AI Template` to generate local message suggestions and copy them into a selected profile. This does not send anything to Microsoft 365.

## Audience Scope

The _Audience Scope_ dropdown controls who receives your external OOF message. There are three options:

- **None** – No external OOF message is sent to anyone outside your organization. When this option is selected, the External Message editor is disabled.
- **Contacts Only** – Only senders who are in your contacts list will receive the external OOF message.
- **All** – All external senders will receive the external OOF message.

_Hint: If you do not want to send an external OOF message at all, set the Audience Scope to **None**._

Internal users always receive the internal reply; `Audience Scope` only controls external automatic replies.

## Repository layout

- `OOFSponderModern/` - WPF app source.
- `OOFSponderModern.Tests/` - lightweight scheduler regression tests.
- `SupportingFiles/publish-modern-github.ps1` - local publish script used by GitHub Releases.
- `.github/workflows/release.yml` - release workflow for `OOFSponderModern-win-x64.zip`.
