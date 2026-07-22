using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using OOFSponderModern.Models;

namespace OOFSponderModern.Services;

public sealed class GraphMailboxSettingsClient : IMailboxSettingsClient
{
    private const string ClientId = "c0eceb27-8cd3-4bb8-9271-c90596069f74";
    private static readonly string[] Scopes = ["user.read", "MailboxSettings.ReadWrite"];
    private static readonly Uri MailboxSettingsUri = new("https://graph.microsoft.com/v1.0/me/mailboxSettings");
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _tokenCacheInitializationLock = new(1, 1);
    private IPublicClientApplication? _publicClientApplication;
    private MsalCacheHelper? _cacheHelper;

    public async Task<CurrentMailboxSettingsSummary> LoadCurrentSettingsAsync(CancellationToken cancellationToken = default)
    {
        var result = await AcquireTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, MailboxSettingsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph GET failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");
        }

        return ParseCurrentSettings(responseBody, result.Account.Username);
    }

    public async Task<string> ApplyAsync(MailboxSettingsPreview preview, CancellationToken cancellationToken = default)
    {
        var result = await AcquireTokenAsync(cancellationToken);
        await ApplyWithTokenAsync(preview, result, cancellationToken);

        return $"Microsoft 365 automatic replies updated for {result.Account.Username}. {preview.ActiveProfile} profile applied from {preview.Window.Start:g} to {preview.Window.End:g} with audience {preview.AudienceScope}. Message bodies omitted.";
    }

    public async Task<AutomaticSyncResult> SyncIfChangedAsync(
        MailboxSettingsPreview preview,
        CancellationToken cancellationToken = default)
    {
        var result = await AcquireTokenSilentAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, MailboxSettingsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph GET failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        if (SettingsMatch(responseBody, preview, DateTimeOffset.Now))
        {
            return new AutomaticSyncResult(false, result.Account.Username);
        }

        await ApplyWithTokenAsync(preview, result, cancellationToken);
        return new AutomaticSyncResult(true, result.Account.Username);
    }

    private async Task ApplyWithTokenAsync(
        MailboxSettingsPreview preview,
        AuthenticationResult result,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), MailboxSettingsUri)
        {
            Content = new StringContent(CreatePayload(preview), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph PATCH failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }

    private async Task<AuthenticationResult> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var publicClientApplication = await GetPublicClientApplicationAsync(cancellationToken);
        var accounts = await publicClientApplication.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account is not null)
        {
            try
            {
                return await publicClientApplication.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken);
            }
            catch (MsalUiRequiredException)
            {
            }
        }

        return await publicClientApplication
            .AcquireTokenInteractive(Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);
    }

    private async Task<AuthenticationResult> AcquireTokenSilentAsync(CancellationToken cancellationToken)
    {
        var publicClientApplication = await GetPublicClientApplicationAsync(cancellationToken);
        var account = (await publicClientApplication.GetAccountsAsync()).FirstOrDefault();
        if (account is null)
        {
            throw new AutomaticSyncAuthenticationRequiredException();
        }

        try
        {
            return await publicClientApplication.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            throw new AutomaticSyncAuthenticationRequiredException();
        }
    }

    private async Task<IPublicClientApplication> GetPublicClientApplicationAsync(CancellationToken cancellationToken)
    {
        if (_publicClientApplication is not null)
        {
            return _publicClientApplication;
        }

        await _tokenCacheInitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_publicClientApplication is not null)
            {
                return _publicClientApplication;
            }

            var publicClientApplication = BuildPublicClientApplication();
            _cacheHelper = await CreateCacheHelperAsync();
            _cacheHelper.RegisterCache(publicClientApplication.UserTokenCache);
            _publicClientApplication = publicClientApplication;
            return publicClientApplication;
        }
        finally
        {
            _tokenCacheInitializationLock.Release();
        }
    }

    private static IPublicClientApplication BuildPublicClientApplication()
    {
        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
        return PublicClientApplicationBuilder
            .Create(ClientId)
            .WithDefaultRedirectUri()
            .WithParentActivityOrWindow(() => Process.GetCurrentProcess().MainWindowHandle)
            .WithBroker(brokerOptions)
            .Build();
    }

    private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
    {
        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OOFSponderModern");
        Directory.CreateDirectory(cacheDirectory);

        var storageProperties = new StorageCreationPropertiesBuilder("msalcache.bin3", cacheDirectory).Build();

        return await MsalCacheHelper.CreateAsync(storageProperties);
    }

    private static string CreatePayload(MailboxSettingsPreview preview)
    {
        var payload = new
        {
            automaticRepliesSetting = new
            {
                status = "scheduled",
                externalAudience = ToGraphAudience(preview.AudienceScope),
                scheduledStartDateTime = ToGraphDateTime(preview.Window.Start),
                scheduledEndDateTime = ToGraphDateTime(preview.Window.End),
                internalReplyMessage = preview.ActiveInternalMessage,
                externalReplyMessage = preview.AudienceScope == AudienceScope.None ? string.Empty : preview.ActiveExternalMessage
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object ToGraphDateTime(DateTimeOffset dateTime)
    {
        var localDateTime = TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Local);
        return new
        {
            dateTime = localDateTime.DateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            timeZone = TimeZoneInfo.Local.Id
        };
    }

    private static string ToGraphAudience(AudienceScope audienceScope) => audienceScope switch
    {
        AudienceScope.None => "none",
        AudienceScope.ContactsOnly => "contactsOnly",
        AudienceScope.AllExternal => "all",
        _ => "contactsOnly"
    };

    internal static bool SettingsMatch(
        string responseBody,
        MailboxSettingsPreview preview,
        DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (!root.TryGetProperty("automaticRepliesSetting", out var setting))
        {
            return false;
        }

        var expectedExternalMessage = preview.AudienceScope == AudienceScope.None
            ? string.Empty
            : preview.ActiveExternalMessage;
        return string.Equals(ReadString(setting, "status"), "scheduled", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ReadString(setting, "externalAudience"), ToGraphAudience(preview.AudienceScope), StringComparison.OrdinalIgnoreCase) &&
             GraphStartDateTimeMatches(setting, preview.Window.Start, now) &&
               GraphDateTimeMatches(setting, "scheduledEndDateTime", preview.Window.End) &&
               string.Equals(ReadString(setting, "internalReplyMessage"), preview.ActiveInternalMessage, StringComparison.Ordinal) &&
               string.Equals(ReadString(setting, "externalReplyMessage"), expectedExternalMessage, StringComparison.Ordinal);
    }

    private static bool GraphStartDateTimeMatches(
        JsonElement setting,
        DateTimeOffset expected,
        DateTimeOffset now)
    {
        if (!TryReadGraphDateTime(setting, "scheduledStartDateTime", out var actualDateTime))
        {
            return false;
        }

        var expectedLocal = TimeZoneInfo.ConvertTime(expected, TimeZoneInfo.Local).DateTime;
        var nowLocal = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime;
        return actualDateTime == expectedLocal ||
               actualDateTime <= nowLocal && expectedLocal <= nowLocal;
    }

    private static bool GraphDateTimeMatches(JsonElement setting, string propertyName, DateTimeOffset expected)
    {
        return TryReadGraphDateTime(setting, propertyName, out var actualDateTime) &&
               actualDateTime == TimeZoneInfo.ConvertTime(expected, TimeZoneInfo.Local).DateTime;
    }

    private static bool TryReadGraphDateTime(
        JsonElement setting,
        string propertyName,
        out DateTime actualDateTime)
    {
        actualDateTime = default;
        if (!setting.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return DateTime.TryParse(
                   ReadString(property, "dateTime"),
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces,
                   out actualDateTime) &&
               string.Equals(ReadString(property, "timeZone"), TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static CurrentMailboxSettingsSummary ParseCurrentSettings(string responseBody, string mailboxUser)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (!root.TryGetProperty("automaticRepliesSetting", out var setting))
        {
            throw new InvalidOperationException("Graph response did not include automaticRepliesSetting.");
        }

        var internalReply = ReadString(setting, "internalReplyMessage");
        var externalReply = ReadString(setting, "externalReplyMessage");

        return new CurrentMailboxSettingsSummary(
            mailboxUser,
            ReadString(setting, "status", "unknown"),
            ReadString(setting, "externalAudience", "unknown"),
            ReadGraphDateTime(setting, "scheduledStartDateTime"),
            ReadGraphDateTime(setting, "scheduledEndDateTime"),
            !string.IsNullOrWhiteSpace(internalReply),
            !string.IsNullOrWhiteSpace(externalReply),
            internalReply.Length,
            externalReply.Length);
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback = "")
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static string ReadGraphDateTime(JsonElement setting, string propertyName)
    {
        if (!setting.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return "not scheduled";
        }

        var dateTime = ReadString(property, "dateTime");
        var timeZone = ReadString(property, "timeZone", "local time");
        return string.IsNullOrWhiteSpace(dateTime) ? "not scheduled" : $"{dateTime} {timeZone}";
    }
}