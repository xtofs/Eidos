using System.Globalization;

namespace Eidos.AspNetCore;

public sealed class EidosRouteMappingOptions
{
    public bool FailOnError { get; set; } = true;

    public Action<EidosRouteDiagnostic>? OnDiagnostic { get; set; }

    public Func<string, string> CollectionSegmentStrategy { get; set; } = DefaultCollectionSegmentName;

    public Func<string, string> ItemRouteParameterStrategy { get; set; } = _ => "key";

    private static string DefaultCollectionSegmentName(string resourceName)
    {
        var lower = resourceName.ToLowerInvariant();

        if (lower.EndsWith("y", true, CultureInfo.InvariantCulture) && lower.Length > 1)
        {
            return lower[..^1] + "ies";
        }

        if (lower.EndsWith("s", true, CultureInfo.InvariantCulture))
        {
            return lower;
        }

        return lower + "s";
    }
}

