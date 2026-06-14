using System;

namespace Eidos.Core.OpenApi;

/// <summary>
/// URL naming conventions used when generating paths (§5.2), and the single source of truth for the
/// default collection-segment rule that Eidos.AspNetCore also uses at runtime.
/// </summary>
public static class ApiNaming
{
    /// <summary>The item route parameter name, e.g. the <c>{key}</c> in <c>/persons/{key}</c>.</summary>
    public const string KeyParameter = "key";

    /// <summary>
    /// Lower-cased, regularly-pluralized collection segment for a type name
    /// (Person → persons, Company → companies, Status → statuses, Key → keys).
    /// Deliberately handles only regular English plurals; irregulars (person → people, child → children)
    /// are the designer's call via the <c>url:</c> hint on the declaration.
    /// </summary>
    public static string CollectionSegment(string typeName)
    {
        var lower = typeName.ToLowerInvariant();
        if (lower.Length == 0)
        {
            return lower;
        }

        // consonant + y → ies (company → companies); vowel + y stays regular (key → keys, day → days)
        if (lower.Length > 1 && lower[^1] == 'y' && !IsVowel(lower[^2]))
        {
            return lower[..^1] + "ies";
        }

        // sibilant endings take -es (status → statuses, box → boxes, dish → dishes, church → churches)
        if (EndsWithAny(lower, "s", "x", "z", "ch", "sh"))
        {
            return lower + "es";
        }

        return lower + "s";
    }

    private static bool EndsWithAny(string value, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // vowels in order of frequency in English text: E, A, O, I, U
    private static bool IsVowel(char c) => "eaoiu".IndexOf(char.ToLowerInvariant(c)) >= 0;
}
