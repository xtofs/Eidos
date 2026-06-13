using System.Globalization;

namespace Eidos.Core.OpenApi;

/// <summary>
/// URL naming conventions used when generating paths (§5.2). Mirrors the default route conventions in
/// Eidos.AspNetCore so generated paths line up with the runtime routes. Kept here so the core generator
/// is self-contained.
/// </summary>
public static class ApiNaming
{
    /// <summary>The item route parameter name, e.g. the <c>{key}</c> in <c>/persons/{key}</c>.</summary>
    public const string KeyParameter = "key";

    /// <summary>Naive pluralized, lower-cased collection segment for a type name (Person → persons, Company → companies).</summary>
    public static string CollectionSegment(string typeName)
    {
        var lower = typeName.ToLowerInvariant();

        if (lower.Length > 1 && lower.EndsWith("y", StringComparison.Ordinal))
        {
            return lower[..^1] + "ies";
        }

        if (lower.EndsWith("s", StringComparison.Ordinal))
        {
            return lower;
        }

        return lower + "s";
    }
}
