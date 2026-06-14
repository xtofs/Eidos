using Eidos.Core.OpenApi;

namespace Eidos.AspNetCore;

public sealed class EidosRouteMappingOptions
{
    public bool FailOnError { get; set; } = true;

    public Action<EidosRouteDiagnostic>? OnDiagnostic { get; set; }

    /// <summary>
    /// Maps a type/relationship name to its collection segment. Defaults to <see cref="ApiNaming.CollectionSegmentName"/>
    /// (the same regular pluralizer the OpenAPI generator uses); a declaration's <c>url:</c> hint overrides it.
    /// </summary>
    public Func<string, string> CollectionSegmentStrategy { get; set; } = ApiNaming.CollectionSegmentName;

    public Func<string, string> ItemRouteParameterStrategy { get; set; } = _ => ApiNaming.KeyParameter;
}
