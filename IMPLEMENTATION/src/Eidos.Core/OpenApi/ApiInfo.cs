namespace Eidos.Core.OpenApi;

/// <summary>Document-level metadata (title + version) for a generated OpenAPI document.</summary>
public sealed record ApiInfo(string Title, string Version);
