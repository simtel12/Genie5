namespace Genie.Core.Profiles;

/// <summary>
/// Single source of truth for the user-visible character label. Two characters
/// can share a name across different accounts, so everywhere we surface a
/// character to the user we show <c>Character-Account</c> (e.g.
/// <c>Renucci-MONIL</c>) rather than the bare name. Use <see cref="Format"/>
/// (or the <see cref="DisplayName"/> extension) instead of interpolating the
/// character name ad-hoc, so the format stays consistent across the title bar,
/// connect dialog, profile picker, and dock titles. (Pre-publish checklist #4.)
/// </summary>
public static class CharacterIdentity
{
    /// <summary><c>Character-Account</c> when both are present; just the
    /// character when there's no account; just the account (or empty) when
    /// there's no character. Trims both, so blank fields never leave a dangling
    /// separator.</summary>
    public static string Format(string? character, string? account)
    {
        character = character?.Trim() ?? string.Empty;
        account   = account?.Trim()   ?? string.Empty;
        if (character.Length == 0) return account;
        return account.Length == 0 ? character : $"{character}-{account}";
    }

    /// <summary><c>Character-Account</c> label for a saved profile.</summary>
    public static string DisplayName(this ConnectionProfile profile)
        => Format(profile.CharacterName, profile.AccountName);
}
