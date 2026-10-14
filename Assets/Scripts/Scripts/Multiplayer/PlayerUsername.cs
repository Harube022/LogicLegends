using System;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>The public name shared by the account profile and Photon players.</summary>
public static class PlayerUsername
{
    public enum ValidationResult { Valid, Empty, TooShort, TooLong, InvalidCharacters, Inappropriate }

    public const int MinLength = 3;
    public const int MaxLength = 20;
    private static string accountId;
    private static string verifiedName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Clear();

    public static void SetVerified(string userId, string username)
    {
        if (!TryNormalize(username, out string normalized)) return;
        accountId = userId;
        verifiedName = normalized;
    }

    public static bool TryGetVerified(string userId, out string username)
    {
        username = accountId == userId ? verifiedName : null;
        return !string.IsNullOrEmpty(username);
    }

    public static void Clear()
    {
        accountId = null;
        verifiedName = null;
    }

    public static bool TryNormalize(string value, out string username)
        => Validate(value, out username) == ValidationResult.Valid;

    public static ValidationResult Validate(string value, out string username)
    {
        username = (value ?? string.Empty).Trim();
        if (username.Length == 0) return ValidationResult.Empty;
        try { username = username.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { return ValidationResult.InvalidCharacters; }

        // Count visible text elements, so a letter plus its combining accent counts as one.
        int length = StringInfo.ParseCombiningCharacters(username).Length;
        if (length < MinLength) return ValidationResult.TooShort;
        if (length > MaxLength) return ValidationResult.TooLong;

        bool canCombine = false;
        for (int i = 0; i < username.Length; i++)
        {
            char c = username[i];
            if (c == '_') { canCombine = false; continue; }
            if (char.IsLowSurrogate(c)) return ValidationResult.InvalidCharacters;
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= username.Length || !char.IsLowSurrogate(username[i + 1]))
                    return ValidationResult.InvalidCharacters;
            }
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(username, i);
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                    canCombine = true;
                    break;
                case UnicodeCategory.DecimalDigitNumber:
                    canCombine = false;
                    break;
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                    if (!canCombine) return ValidationResult.InvalidCharacters;
                    break;
                default:
                    return ValidationResult.InvalidCharacters;
            }
            if (char.IsHighSurrogate(c)) i++;
        }

        if (InappropriateTextFilter.IsBlockedName(username)) return ValidationResult.Inappropriate;
        return ValidationResult.Valid;
    }

    public static string Feedback(ValidationResult result)
    {
        switch (result)
        {
            case ValidationResult.Empty: return "Enter a username to continue.";
            case ValidationResult.TooShort: return "Use at least 3 characters.";
            case ValidationResult.TooLong: return "Use no more than 20 characters.";
            case ValidationResult.InvalidCharacters: return "Use letters, numbers, or _. No spaces.";
            case ValidationResult.Inappropriate: return "That username isn't allowed. Try another.";
            default: return "This username looks good.";
        }
    }
}
