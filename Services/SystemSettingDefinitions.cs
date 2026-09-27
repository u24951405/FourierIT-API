namespace FourierIT_API.Services;

public enum SettingUnit
{
    Seconds,
    Minutes,
    Hours,
    Days,
    Count
}

/// <summary>A timer or limit the Super Admin can change on the System Settings page.</summary>
public sealed record SystemSettingDefinition(
    string Key,
    string Category,
    string Title,
    string Description,
    SettingUnit Unit,
    int Default,
    int Min,
    int Max);

/// <summary>
/// The single list of configurable timers and limits: used to seed the database, validate changes,
/// read values at runtime and describe the settings to the System Settings page.
/// </summary>
public static class SystemSettingDefinitions
{
    public const string VerificationCodes = "Verification codes";
    public const string Sessions = "Sessions";
    public const string Links = "Links";
    public const string SecurityLimits = "Security limits";
    public const string Reminders = "Reminders";

    public const string UserOtpExpiryMinutes = "UserOtpExpiryMinutes";
    public const string InstitutionOtpExpiryMinutes = "InstitutionOtpExpiryMinutes";
    public const string StaffSessionTimeoutHours = "StaffSessionTimeoutHours";
    public const string InstitutionSessionTimeoutMinutes = "InstitutionSessionTimeoutMinutes";
    public const string InstitutionInvitationExpiryDays = "InstitutionInvitationExpiryDays";
    public const string PasswordResetLinkExpiryMinutes = "PasswordResetLinkExpiryMinutes";
    public const string DocumentAccessLinkExpiryHours = "DocumentAccessLinkExpiryHours";
    public const string MaxCodeAttempts = "MaxCodeAttempts";
    public const string CodeResendCooldownSeconds = "CodeResendCooldownSeconds";
    public const string OwnerReminderAfterHours = "OwnerReminderAfterHours";
    public const string AccessExpiryReminderHours = "AccessExpiryReminderHours";
    public const string MaxAccessExtensionDays = "MaxAccessExtensionDays";

    public static readonly IReadOnlyList<SystemSettingDefinition> All = new[]
    {
        new SystemSettingDefinition(UserOtpExpiryMinutes, VerificationCodes,
            "User verification code",
            "How long the code emailed when someone registers or changes their email stays valid.",
            SettingUnit.Minutes, Default: 10, Min: 5, Max: 30),
        new SystemSettingDefinition(InstitutionOtpExpiryMinutes, VerificationCodes,
            "Institution sign-in code",
            "How long the code emailed to an institution when it opens its access link stays valid.",
            SettingUnit.Minutes, Default: 10, Min: 5, Max: 15),

        new SystemSettingDefinition(StaffSessionTimeoutHours, Sessions,
            "Staff sign-in session",
            "How long staff stay signed in before they must sign in again.",
            SettingUnit.Hours, Default: 8, Min: 1, Max: 24),
        new SystemSettingDefinition(InstitutionSessionTimeoutMinutes, Sessions,
            "Institution portal session",
            "How long an institution stays signed in to the portal after entering its code.",
            SettingUnit.Minutes, Default: 60, Min: 15, Max: 240),

        new SystemSettingDefinition(InstitutionInvitationExpiryDays, Links,
            "Institution invitation link",
            "How long an emailed institution access link can be used before a new one is needed.",
            SettingUnit.Days, Default: 7, Min: 1, Max: 30),
        new SystemSettingDefinition(PasswordResetLinkExpiryMinutes, Links,
            "Password reset link",
            "How long a password reset link stays valid.",
            SettingUnit.Minutes, Default: 60, Min: 15, Max: 60),
        new SystemSettingDefinition(DocumentAccessLinkExpiryHours, Links,
            "Document access link",
            "How long the access link issued for an institution's document request stays valid.",
            SettingUnit.Hours, Default: 48, Min: 24, Max: 168),

        new SystemSettingDefinition(MaxCodeAttempts, SecurityLimits,
            "Wrong code attempts",
            "How many incorrect verification codes are allowed before the code is cancelled and a new one is needed.",
            SettingUnit.Count, Default: 5, Min: 3, Max: 10),
        new SystemSettingDefinition(CodeResendCooldownSeconds, SecurityLimits,
            "Wait before resending a code",
            "How long someone must wait before another verification code or access link can be emailed.",
            SettingUnit.Seconds, Default: 60, Min: 30, Max: 300),

        new SystemSettingDefinition(OwnerReminderAfterHours, Reminders,
            "Remind owners about unanswered requests",
            "How long a document request can wait for an answer before the owner (or department admin) is reminded.",
            SettingUnit.Hours, Default: 48, Min: 12, Max: 168),
        new SystemSettingDefinition(AccessExpiryReminderHours, Reminders,
            "Warn institutions before access ends",
            "How long before an institution's access to documents ends that it is emailed a reminder.",
            SettingUnit.Hours, Default: 24, Min: 2, Max: 72),
        new SystemSettingDefinition(MaxAccessExtensionDays, Reminders,
            "Longest access extension",
            "The most extra time an institution can ask for when it needs approved documents for longer.",
            SettingUnit.Days, Default: 7, Min: 1, Max: 30),
    };

    public static SystemSettingDefinition? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
}
