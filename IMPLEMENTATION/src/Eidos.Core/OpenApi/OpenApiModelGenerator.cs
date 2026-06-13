using System;
using System.Collections.Generic;
using System.Linq;

namespace Eidos.Core.OpenApi;

/// <summary>
/// Produces a provider-neutral <see cref="OpenApiModel"/> from an Eidos schema, applying the §5.2
/// HTTP-surface mapping rules. Pure and dependency-free; a host adapts the result to a concrete OpenAPI
/// document. Scope (v1): entities + relationships → paths, component schemas, and request bodies for
/// create / JSON Patch property update / state transition, plus the relationship <c>?expand</c> parameter.
/// Archetype-based operation pruning and writable-in/visible-in per-state schemas are not yet applied.
/// </summary>
public static class OpenApiModelGenerator
{
    private static readonly string[] JsonPatchOps = ["add", "remove", "replace", "move", "copy", "test"];

    public static OpenApiModel Generate(EidosDocumentSyntax document, ApiInfo info)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(info);

        var archetypes = document.Declarations
            .OfType<ArchetypeDeclarationSyntax>()
            .GroupBy(a => a.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entities = document.Declarations.OfType<EntityDeclarationSyntax>().ToList();
        var relationships = document.Declarations.OfType<RelationshipDeclarationSyntax>().ToList();

        var components = new List<ApiNamedSchema>();
        var paths = new List<ApiPath>();

        foreach (var entity in entities)
        {
            var lifecycle = AnalyzeLifecycle(EntityLifecycle(entity), archetypes);
            components.Add(new ApiNamedSchema(entity.Name, BuildObjectSchema(EntityProperties(entity), participants: [], lifecycle.HasLifecycle)));
            paths.AddRange(ResourcePaths(entity.Name, lifecycle));
        }

        foreach (var relationship in relationships)
        {
            var lifecycle = AnalyzeLifecycle(RelationshipLifecycle(relationship), archetypes);
            components.Add(new ApiNamedSchema(relationship.Name, BuildObjectSchema(RelationshipProperties(relationship), relationship.Participants, lifecycle.HasLifecycle)));
            var expand = relationship.Participants.Select(p => p.Name).ToList();
            paths.AddRange(ResourcePaths(relationship.Name, lifecycle, expand));
            paths.AddRange(ProjectionPaths(relationship));
        }

        if (relationships.Count > 0 || entities.Count > 0)
        {
            components.Add(JsonPatchComponent());
        }

        return new OpenApiModel(info, paths, components);
    }

    // ── Paths ───────────────────────────────────────────────────────────────

    private static IEnumerable<ApiPath> ResourcePaths(
        string typeName,
        LifecycleInfo lifecycle,
        IReadOnlyList<string>? expandParticipants = null)
    {
        var coll = ApiNaming.CollectionSegment(typeName);
        var item = $"/{coll}/{{{ApiNaming.KeyParameter}}}";

        yield return new ApiPath($"/{coll}", [ListOp(typeName), CreateOp(typeName)]);

        var itemOps = new List<ApiOperation> { GetOp(typeName, expandParticipants), PatchOp(typeName), DeleteOp(typeName) };
        yield return new ApiPath(item, itemOps);

        if (lifecycle.HasLifecycle)
        {
            yield return new ApiPath($"{item}/_state", [SetStateOp(typeName, lifecycle)]);
        }
    }

    private static IEnumerable<ApiPath> ProjectionPaths(RelationshipDeclarationSyntax relationship)
    {
        var coll = ApiNaming.CollectionSegment(relationship.Name);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var participant in relationship.Participants)
        {
            var template = $"/{ApiNaming.CollectionSegment(participant.TypeName)}/{{{ApiNaming.KeyParameter}}}/{coll}";
            if (!seen.Add(template))
            {
                continue;
            }

            var op = new ApiOperation(
                ApiMethod.Get,
                $"list{relationship.Name}By{Capitalize(participant.Name)}",
                $"List {relationship.Name} where the {participant.TypeName} plays {participant.Name}",
                [KeyParam()],
                RequestBody: null,
                [Ok(ArrayOf(relationship.Name))]);

            yield return new ApiPath(template, [op]);
        }
    }

    private static ApiOperation ListOp(string type) => new(
        ApiMethod.Get, $"list{type}", $"List {type}", [], null, [Ok(ArrayOf(type))]);

    private static ApiOperation CreateOp(string type) => new(
        ApiMethod.Post, $"create{type}", $"Create a {type}", [],
        new ApiRequestBody("application/json", Ref(type), Required: true),
        [Created(type), BadRequest(), Conflict()]);

    private static ApiOperation GetOp(string type, IReadOnlyList<string>? expandParticipants = null)
    {
        var parameters = new List<ApiParameter> { KeyParam() };
        if (expandParticipants is { Count: > 0 })
        {
            parameters.Add(new ApiParameter(
                "expand", ApiParameterLocation.Query, Required: false,
                new ApiEnumSchema(expandParticipants),
                "Embed the named participant(s) inline instead of as URLs (comma-separated)."));
        }

        return new ApiOperation(ApiMethod.Get, $"get{type}", $"Get a {type} by key", parameters, null, [Ok(Ref(type)), NotFound()]);
    }

    private static ApiOperation PatchOp(string type) => new(
        ApiMethod.Patch, $"patch{type}", $"Update {type} properties via JSON Patch (RFC 6902)", [KeyParam()],
        new ApiRequestBody("application/json-patch+json", new ApiReferenceSchema("JsonPatch"), Required: true),
        [Ok(Ref(type)), BadRequest(), NotFound()]);

    private static ApiOperation DeleteOp(string type) => new(
        ApiMethod.Delete, $"delete{type}", $"Delete a {type}", [KeyParam()], null,
        [new ApiResponse(204, "Deleted"), NotFound()]);

    private static ApiOperation SetStateOp(string type, LifecycleInfo lifecycle) => new(
        ApiMethod.Put, $"set{type}State", $"Transition the lifecycle state of a {type}", [KeyParam()],
        new ApiRequestBody("application/json", StateBody(lifecycle), Required: true),
        [Ok(Ref(type)), BadRequest(), NotFound(), Conflict()]);

    // ── Request/response building blocks ──────────────────────────────────────

    private static ApiObjectSchema StateBody(LifecycleInfo lifecycle)
    {
        var props = new List<ApiProperty>
        {
            new("state",
                lifecycle.StatesKnown && lifecycle.States.Count > 0
                    ? new ApiEnumSchema(lifecycle.States)
                    : new ApiScalarSchema(ApiScalarType.String),
                Required: true)
        };

        // Non-deterministic machines need 'transition' to disambiguate a shared (source, target) target.
        if (lifecycle.NonDeterministic && lifecycle.Transitions.Count > 0)
        {
            props.Add(new ApiProperty("transition", new ApiEnumSchema(lifecycle.Transitions), Required: true));
        }

        return new ApiObjectSchema(props);
    }

    private static ApiParameter KeyParam() =>
        new(ApiNaming.KeyParameter, ApiParameterLocation.Path, Required: true, new ApiScalarSchema(ApiScalarType.String), "Resource key");

    private static ApiResponse Ok(ApiSchema schema) => new(200, "OK", schema);
    private static ApiResponse Created(string type) => new(201, "Created", Ref(type));
    private static ApiResponse BadRequest() => new(400, "Validation problem");
    private static ApiResponse NotFound() => new(404, "Not found");
    private static ApiResponse Conflict() => new(409, "Conflict");

    private static ApiReferenceSchema Ref(string type) => new(type);
    private static ApiArraySchema ArrayOf(string type) => new(new ApiReferenceSchema(type));

    private static ApiNamedSchema JsonPatchComponent() => new(
        "JsonPatch",
        new ApiArraySchema(new ApiObjectSchema([
            new ApiProperty("op", new ApiEnumSchema(JsonPatchOps), Required: true),
            new ApiProperty("path", new ApiScalarSchema(ApiScalarType.String), Required: true),
            new ApiProperty("value", new ApiAnySchema()),
            new ApiProperty("from", new ApiScalarSchema(ApiScalarType.String))
        ])));

    // ── Component schemas ─────────────────────────────────────────────────────

    private static ApiObjectSchema BuildObjectSchema(
        IEnumerable<PropertyDeclarationSyntax> declaredProperties,
        IReadOnlyList<ParticipantSyntax> participants,
        bool hasLifecycle)
    {
        var props = new List<ApiProperty>();

        // Participants are represented as URLs by default (?expand embeds them inline).
        foreach (var participant in participants)
        {
            props.Add(new ApiProperty(participant.Name, new ApiScalarSchema(ApiScalarType.String, "uri"), Required: true));
        }

        foreach (var property in declaredProperties)
        {
            props.Add(MapProperty(property));
        }

        // Reserved system fields emitted by the compiler; never declared by the designer.
        props.Add(new ApiProperty("_self", new ApiScalarSchema(ApiScalarType.String, "uri"), Required: true, ReadOnly: true));
        props.Add(new ApiProperty("_type", new ApiScalarSchema(ApiScalarType.String), Required: true, ReadOnly: true));
        if (hasLifecycle)
        {
            props.Add(new ApiProperty("_state", new ApiScalarSchema(ApiScalarType.String), Required: true, ReadOnly: true));
        }

        return new ApiObjectSchema(props);
    }

    private static ApiProperty MapProperty(PropertyDeclarationSyntax property)
    {
        var (schema, nullableFromType) = MapType(property.Type);
        var flags = property.Options.OfType<PropertyFlagOptionSyntax>().Select(o => o.Flag).ToHashSet();

        return new ApiProperty(
            property.Name,
            schema,
            Required: flags.Contains(PropertyFlagKind.Required),
            Nullable: nullableFromType || flags.Contains(PropertyFlagKind.Nullable),
            ReadOnly: flags.Contains(PropertyFlagKind.ReadOnly));
    }

    private static (ApiSchema Schema, bool Nullable) MapType(TypeExpressionSyntax type) => type switch
    {
        ScalarTypeExpressionSyntax s => (MapScalar(s.Kind), false),
        ReferenceTypeExpressionSyntax => (new ApiScalarSchema(ApiScalarType.String, "uri"), false),
        ListTypeExpressionSyntax l => (new ApiArraySchema(MapType(l.ElementType).Schema), false),
        OptionalTypeExpressionSyntax o => (MapType(o.ElementType).Schema, true),
        EnumTypeExpressionSyntax e => (new ApiEnumSchema(e.Values), false),
        _ => (new ApiAnySchema(), false)
    };

    private static ApiSchema MapScalar(ScalarTypeKind kind) => kind switch
    {
        ScalarTypeKind.String => new ApiScalarSchema(ApiScalarType.String),
        ScalarTypeKind.Integer => new ApiScalarSchema(ApiScalarType.Integer),
        ScalarTypeKind.Number => new ApiScalarSchema(ApiScalarType.Number),
        ScalarTypeKind.Boolean => new ApiScalarSchema(ApiScalarType.Boolean),
        ScalarTypeKind.Date => new ApiScalarSchema(ApiScalarType.String, "date"),
        ScalarTypeKind.DateTime => new ApiScalarSchema(ApiScalarType.String, "date-time"),
        ScalarTypeKind.Time => new ApiScalarSchema(ApiScalarType.String, "time"),
        ScalarTypeKind.Money => MoneySchema(),
        ScalarTypeKind.Email => new ApiScalarSchema(ApiScalarType.String, "email"),
        ScalarTypeKind.Url => new ApiScalarSchema(ApiScalarType.String, "uri"),
        ScalarTypeKind.Uuid => new ApiScalarSchema(ApiScalarType.String, "uuid"),
        _ => new ApiScalarSchema(ApiScalarType.String)
    };

    // Money is represented as { amount, currency } in the spec's examples.
    private static ApiObjectSchema MoneySchema() => new([
        new ApiProperty("amount", new ApiScalarSchema(ApiScalarType.Number), Required: true),
        new ApiProperty("currency", new ApiScalarSchema(ApiScalarType.String), Required: true)
    ]);

    // ── Lifecycle resolution ──────────────────────────────────────────────────

    private static LifecycleInfo AnalyzeLifecycle(
        LifecycleClauseSyntax? clause,
        IReadOnlyDictionary<string, ArchetypeDeclarationSyntax> archetypes)
    {
        if (clause is null)
        {
            return LifecycleInfo.None;
        }

        var inline = ResolveLifecycle(clause, archetypes);
        if (inline is null)
        {
            // Composition or unresolved archetype: it has a lifecycle, but we can't enumerate its states here.
            return new LifecycleInfo(HasLifecycle: true, StatesKnown: false, [], [], NonDeterministic: false);
        }

        var states = inline.Members.OfType<StatesBlockSyntax>()
            .SelectMany(b => b.States).Select(s => s.Name)
            .Distinct(StringComparer.Ordinal).ToList();

        var transitions = inline.Members.OfType<TransitionsBlockSyntax>()
            .SelectMany(b => b.Transitions).ToList();

        var transitionNames = transitions.Select(t => t.Name).Distinct(StringComparer.Ordinal).ToList();

        // Non-deterministic: a (source, target) pair reached by transitions with distinct names.
        var nonDeterministic = transitions
            .SelectMany(t => SourceStates(t).Select(source => (source, t.TargetState, t.Name)))
            .GroupBy(e => (e.source, e.TargetState))
            .Any(g => g.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count() > 1);

        return new LifecycleInfo(HasLifecycle: true, StatesKnown: true, states, transitionNames, nonDeterministic);
    }

    private static InlineLifecycleSyntax? ResolveLifecycle(
        LifecycleClauseSyntax clause,
        IReadOnlyDictionary<string, ArchetypeDeclarationSyntax> archetypes) => clause switch
    {
        InlineLifecycleClauseSyntax inline => inline.Lifecycle,
        ArchetypeReferenceLifecycleSyntax reference when reference.Archetypes.Count == 1
            && archetypes.TryGetValue(reference.Archetypes[0], out var archetype) => archetype.Lifecycle,
        _ => null
    };

    private static IEnumerable<string> SourceStates(TransitionDeclarationSyntax transition) => transition.SourceStates switch
    {
        SingleStateSetSyntax single => [single.StateName],
        MultiStateSetSyntax multi => multi.StateNames,
        _ => []
    };

    private static LifecycleClauseSyntax? EntityLifecycle(EntityDeclarationSyntax entity) =>
        entity.Members.OfType<EntityLifecycleMemberSyntax>().FirstOrDefault()?.Lifecycle;

    private static LifecycleClauseSyntax? RelationshipLifecycle(RelationshipDeclarationSyntax relationship) =>
        relationship.Members.OfType<RelationshipLifecycleMemberSyntax>().FirstOrDefault()?.Lifecycle;

    private static IEnumerable<PropertyDeclarationSyntax> EntityProperties(EntityDeclarationSyntax entity) =>
        entity.Members.OfType<EntityPropertiesMemberSyntax>().SelectMany(m => m.Properties.Properties);

    private static IEnumerable<PropertyDeclarationSyntax> RelationshipProperties(RelationshipDeclarationSyntax relationship) =>
        relationship.Members.OfType<RelationshipPropertiesMemberSyntax>().SelectMany(m => m.Properties.Properties);

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private sealed record LifecycleInfo(
        bool HasLifecycle,
        bool StatesKnown,
        IReadOnlyList<string> States,
        IReadOnlyList<string> Transitions,
        bool NonDeterministic)
    {
        public static readonly LifecycleInfo None = new(false, false, [], [], false);
    }
}
