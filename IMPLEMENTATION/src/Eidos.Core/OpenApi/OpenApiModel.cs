using System.Collections.Generic;

namespace Eidos.Core.OpenApi;

/// <summary>
/// A provider-neutral, strongly-typed description of an HTTP API in OpenAPI shape. Produced by
/// <see cref="OpenApiModelGenerator"/> purely from an Eidos schema; carries no dependency on any
/// concrete OpenAPI library. A host (e.g. Eidos.AspNetCore) transcribes this into a real OpenAPI
/// document.
/// </summary>
public sealed record OpenApiModel(
    ApiInfo Info,
    IReadOnlyList<ApiPath> Paths,
    IReadOnlyList<ApiNamedSchema> Components);

public sealed record ApiInfo(string Title, string Version);

public sealed record ApiPath(string Template, IReadOnlyList<ApiOperation> Operations);

public enum ApiMethod
{
    Get,
    Post,
    Patch,
    Put,
    Delete
}

public sealed record ApiOperation(
    ApiMethod Method,
    string OperationId,
    string? Summary,
    IReadOnlyList<ApiParameter> Parameters,
    ApiRequestBody? RequestBody,
    IReadOnlyList<ApiResponse> Responses);

public enum ApiParameterLocation
{
    Path,
    Query
}

public sealed record ApiParameter(
    string Name,
    ApiParameterLocation In,
    bool Required,
    ApiSchema Schema,
    string? Description = null);

public sealed record ApiRequestBody(string MediaType, ApiSchema Schema, bool Required);

public sealed record ApiResponse(int StatusCode, string Description, ApiSchema? Schema = null);

/// <summary>A named entry in the components/schemas section.</summary>
public sealed record ApiNamedSchema(string Name, ApiSchema Schema);

// ── Schema model (a small JSON-Schema subset) ───────────────────────────────

public abstract record ApiSchema;

public sealed record ApiObjectSchema(IReadOnlyList<ApiProperty> Properties) : ApiSchema;

public sealed record ApiProperty(
    string Name,
    ApiSchema Schema,
    bool Required = false,
    bool Nullable = false,
    bool ReadOnly = false);

public enum ApiScalarType
{
    String,
    Integer,
    Number,
    Boolean
}

/// <summary>A scalar value, optionally with an OpenAPI <c>format</c> hint (e.g. "date-time", "email").</summary>
public sealed record ApiScalarSchema(ApiScalarType Type, string? Format = null) : ApiSchema;

public sealed record ApiArraySchema(ApiSchema Items) : ApiSchema;

/// <summary>A string enumeration (e.g. lifecycle states, transition names).</summary>
public sealed record ApiEnumSchema(IReadOnlyList<string> Values) : ApiSchema;

/// <summary>A reference to a named entry in <see cref="OpenApiModel.Components"/>.</summary>
public sealed record ApiReferenceSchema(string ComponentName) : ApiSchema;

/// <summary>An unconstrained value (OpenAPI empty schema) — e.g. a JSON Patch operation's <c>value</c>.</summary>
public sealed record ApiAnySchema : ApiSchema;
