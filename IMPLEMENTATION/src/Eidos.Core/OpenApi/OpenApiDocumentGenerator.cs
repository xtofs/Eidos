using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Eidos.Core.OpenApi;

/// <summary>
/// Generates a Microsoft.OpenApi <see cref="OpenApiDocument"/> from an Eidos schema, applying the §5.2
/// HTTP-surface mapping rules. Scope (v1): entities + relationships → paths, component schemas, and request
/// bodies for create / JSON Patch property update / state transition, plus the relationship <c>?expand</c>
/// parameter. Archetype-based operation pruning and writable-in/visible-in per-state schemas are not yet applied.
/// </summary>
public static class OpenApiDocumentGenerator
{
    private static readonly string[] JsonPatchOps = ["add", "remove", "replace", "move", "copy", "test"];

    public static OpenApiDocument Generate(EidosDocumentSyntax document, ApiInfo info)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(info);

        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = info.Title, Version = info.Version },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            }
        };

        var archetypes = document.Declarations
            .OfType<ArchetypeDeclarationSyntax>()
            .GroupBy(a => a.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entities = document.Declarations.OfType<EntityDeclarationSyntax>().ToList();
        var relationships = document.Declarations.OfType<RelationshipDeclarationSyntax>().ToList();

        // Resolve each type's collection segment once, honoring a declaration's `url:` hint over the default.
        var segmentByType = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            segmentByType[entity.Name] = UrlHint(entity.Members.OfType<EntityUrlHintMemberSyntax>().Select(m => m.UrlHint))
                ?? ApiNaming.CollectionSegment(entity.Name);
        }

        foreach (var relationship in relationships)
        {
            segmentByType[relationship.Name] = UrlHint(relationship.Members.OfType<RelationshipUrlHintMemberSyntax>().Select(m => m.UrlHint))
                ?? ApiNaming.CollectionSegment(relationship.Name);
        }

        string Segment(string typeName) => segmentByType.TryGetValue(typeName, out var s) ? s : ApiNaming.CollectionSegment(typeName);

        foreach (var entity in entities)
        {
            var lifecycle = AnalyzeLifecycle(EntityLifecycle(entity), archetypes);
            doc.Components.Schemas[entity.Name] = BuildObjectSchema(EntityProperties(entity), [], lifecycle.HasLifecycle);
            AddResourcePaths(doc, entity.Name, Segment(entity.Name), lifecycle, expandParticipants: null);
        }

        foreach (var relationship in relationships)
        {
            var lifecycle = AnalyzeLifecycle(RelationshipLifecycle(relationship), archetypes);
            doc.Components.Schemas[relationship.Name] = BuildObjectSchema(RelationshipProperties(relationship), relationship.Participants, lifecycle.HasLifecycle);
            AddResourcePaths(doc, relationship.Name, Segment(relationship.Name), lifecycle, relationship.Participants.Select(p => p.Name).ToList());
            AddProjectionPaths(doc, relationship, Segment);
        }

        if (entities.Count > 0 || relationships.Count > 0)
        {
            doc.Components.Schemas["JsonPatch"] = JsonPatchSchema();
        }

        return doc;
    }

    // ── Paths ───────────────────────────────────────────────────────────────

    private static void AddResourcePaths(OpenApiDocument doc, string typeName, string coll, LifecycleInfo lifecycle, IReadOnlyList<string>? expandParticipants)
    {
        var item = $"/{coll}/{{{ApiNaming.KeyParameter}}}";

        AddPath(doc, $"/{coll}", pathItem =>
        {
            pathItem.AddOperation(HttpMethod.Get, ListOp(typeName, doc));
            pathItem.AddOperation(HttpMethod.Post, CreateOp(typeName, doc));
        });

        AddPath(doc, item, pathItem =>
        {
            pathItem.AddOperation(HttpMethod.Get, GetOp(typeName, doc, expandParticipants));
            pathItem.AddOperation(HttpMethod.Patch, PatchOp(typeName, doc));
            pathItem.AddOperation(HttpMethod.Delete, DeleteOp(typeName, doc));
        });

        if (lifecycle.HasLifecycle)
        {
            AddPath(doc, $"{item}/_state", pathItem => pathItem.AddOperation(HttpMethod.Put, SetStateOp(typeName, lifecycle, doc)));
        }
    }

    private static void AddProjectionPaths(OpenApiDocument doc, RelationshipDeclarationSyntax relationship, Func<string, string> segment)
    {
        var coll = segment(relationship.Name);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var participant in relationship.Participants)
        {
            var template = $"/{segment(participant.TypeName)}/{{{ApiNaming.KeyParameter}}}/{coll}";
            if (!seen.Add(template))
            {
                continue;
            }

            var op = new OpenApiOperation
            {
                OperationId = $"list{relationship.Name}By{Capitalize(participant.Name)}",
                Summary = $"List {relationship.Name} where the {participant.TypeName} plays {participant.Name}",
                Parameters = [KeyParam()],
                Responses = Responses((200, "OK", ArrayOf(relationship.Name, doc)))
            };

            AddPath(doc, template, pathItem => pathItem.AddOperation(HttpMethod.Get, op));
        }
    }

    private static void AddPath(OpenApiDocument doc, string template, Action<OpenApiPathItem> configure)
    {
        var pathItem = new OpenApiPathItem();
        configure(pathItem);
        doc.Paths[template] = pathItem;
    }

    private static OpenApiOperation ListOp(string type, OpenApiDocument doc) => new()
    {
        OperationId = $"list{type}",
        Summary = $"List {type}",
        Responses = Responses((200, "OK", ArrayOf(type, doc)))
    };

    private static OpenApiOperation CreateOp(string type, OpenApiDocument doc) => new()
    {
        OperationId = $"create{type}",
        Summary = $"Create a {type}",
        RequestBody = JsonBody(Ref(type, doc)),
        Responses = Responses((201, "Created", Ref(type, doc)), (400, "Validation problem", null), (409, "Conflict", null))
    };

    private static OpenApiOperation GetOp(string type, OpenApiDocument doc, IReadOnlyList<string>? expandParticipants)
    {
        var op = new OpenApiOperation
        {
            OperationId = $"get{type}",
            Summary = $"Get a {type} by key",
            Parameters = [KeyParam()],
            Responses = Responses((200, "OK", Ref(type, doc)), (404, "Not found", null))
        };

        if (expandParticipants is { Count: > 0 })
        {
            op.Parameters!.Add(new OpenApiParameter
            {
                Name = "expand",
                In = ParameterLocation.Query,
                Required = false,
                Description = "Embed the named participant(s) inline instead of as URLs (comma-separated).",
                Schema = EnumSchema(expandParticipants)
            });
        }

        return op;
    }

    private static OpenApiOperation PatchOp(string type, OpenApiDocument doc) => new()
    {
        OperationId = $"patch{type}",
        Summary = $"Update {type} properties via JSON Patch (RFC 6902)",
        Parameters = [KeyParam()],
        RequestBody = Body("application/json-patch+json", Ref("JsonPatch", doc)),
        Responses = Responses((200, "OK", Ref(type, doc)), (400, "Validation problem", null), (404, "Not found", null))
    };

    private static OpenApiOperation DeleteOp(string type, OpenApiDocument doc) => new()
    {
        OperationId = $"delete{type}",
        Summary = $"Delete a {type}",
        Parameters = [KeyParam()],
        Responses = Responses((204, "Deleted", null), (404, "Not found", null))
    };

    private static OpenApiOperation SetStateOp(string type, LifecycleInfo lifecycle, OpenApiDocument doc) => new()
    {
        OperationId = $"set{type}State",
        Summary = $"Transition the lifecycle state of a {type}",
        Parameters = [KeyParam()],
        RequestBody = JsonBody(StateBody(lifecycle)),
        Responses = Responses((200, "OK", Ref(type, doc)), (400, "Validation problem", null), (404, "Not found", null), (409, "Conflict", null))
    };

    // ── Building blocks ───────────────────────────────────────────────────────

    private static OpenApiParameter KeyParam() => new()
    {
        Name = ApiNaming.KeyParameter,
        In = ParameterLocation.Path,
        Required = true,
        Description = "Resource key",
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    };

    private static OpenApiRequestBody JsonBody(IOpenApiSchema schema) => Body("application/json", schema);

    private static OpenApiRequestBody Body(string mediaType, IOpenApiSchema schema) => new()
    {
        Required = true,
        Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
        {
            [mediaType] = new OpenApiMediaType { Schema = schema }
        }
    };

    private static OpenApiResponses Responses(params (int Status, string Description, IOpenApiSchema? Schema)[] responses)
    {
        var result = new OpenApiResponses();
        foreach (var (status, description, schema) in responses)
        {
            var response = new OpenApiResponse { Description = description };
            if (schema is not null)
            {
                response.Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/json"] = new OpenApiMediaType { Schema = schema }
                };
            }

            result[status.ToString()] = response;
        }

        return result;
    }

    private static IOpenApiSchema Ref(string componentName, OpenApiDocument doc) => new OpenApiSchemaReference(componentName, doc);

    private static IOpenApiSchema ArrayOf(string componentName, OpenApiDocument doc) =>
        new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference(componentName, doc) };

    private static OpenApiSchema EnumSchema(IReadOnlyList<string> values) => new()
    {
        Type = JsonSchemaType.String,
        Enum = values.Select(v => (JsonNode)JsonValue.Create(v)).ToList()
    };

    private static OpenApiSchema StateBody(LifecycleInfo lifecycle)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
            Required = new HashSet<string>(StringComparer.Ordinal)
        };

        schema.Properties["state"] = lifecycle.StatesKnown && lifecycle.States.Count > 0
            ? EnumSchema(lifecycle.States)
            : new OpenApiSchema { Type = JsonSchemaType.String };
        schema.Required.Add("state");

        // Non-deterministic machines need 'transition' to disambiguate a shared (source, target) target.
        if (lifecycle.NonDeterministic && lifecycle.Transitions.Count > 0)
        {
            schema.Properties["transition"] = EnumSchema(lifecycle.Transitions);
            schema.Required.Add("transition");
        }

        return schema;
    }

    private static OpenApiSchema JsonPatchSchema()
    {
        var operation = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["op"] = EnumSchema(JsonPatchOps),
                ["path"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["value"] = new OpenApiSchema(),
                ["from"] = new OpenApiSchema { Type = JsonSchemaType.String }
            },
            Required = new HashSet<string>(StringComparer.Ordinal) { "op", "path" }
        };

        return new OpenApiSchema { Type = JsonSchemaType.Array, Items = operation };
    }

    // ── Component schemas ─────────────────────────────────────────────────────

    private static OpenApiSchema BuildObjectSchema(
        IEnumerable<PropertyDeclarationSyntax> declaredProperties,
        IReadOnlyList<ParticipantSyntax> participants,
        bool hasLifecycle)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
            Required = new HashSet<string>(StringComparer.Ordinal)
        };

        // Participants are represented as URLs by default (?expand embeds them inline).
        foreach (var participant in participants)
        {
            schema.Properties[participant.Name] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" };
            schema.Required.Add(participant.Name);
        }

        foreach (var property in declaredProperties)
        {
            var (propertySchema, required) = MapProperty(property);
            schema.Properties[property.Name] = propertySchema;
            if (required)
            {
                schema.Required.Add(property.Name);
            }
        }

        // Reserved system fields emitted by the compiler; never declared by the designer.
        schema.Properties["_self"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri", ReadOnly = true };
        schema.Required.Add("_self");
        schema.Properties["_type"] = new OpenApiSchema { Type = JsonSchemaType.String, ReadOnly = true };
        schema.Required.Add("_type");
        if (hasLifecycle)
        {
            schema.Properties["_state"] = new OpenApiSchema { Type = JsonSchemaType.String, ReadOnly = true };
            schema.Required.Add("_state");
        }

        return schema;
    }

    private static (IOpenApiSchema Schema, bool Required) MapProperty(PropertyDeclarationSyntax property)
    {
        var (schema, nullableFromType) = MapType(property.Type);
        var flags = property.Options.OfType<PropertyFlagOptionSyntax>().Select(o => o.Flag).ToHashSet();

        if (schema is OpenApiSchema concrete)
        {
            if ((nullableFromType || flags.Contains(PropertyFlagKind.Nullable)) && concrete.Type is { } type)
            {
                concrete.Type = type | JsonSchemaType.Null;
            }

            if (flags.Contains(PropertyFlagKind.ReadOnly))
            {
                concrete.ReadOnly = true;
            }
        }

        return (schema, flags.Contains(PropertyFlagKind.Required));
    }

    private static (IOpenApiSchema Schema, bool Nullable) MapType(TypeExpressionSyntax type) => type switch
    {
        ScalarTypeExpressionSyntax s => (MapScalar(s.Kind), false),
        ReferenceTypeExpressionSyntax => (new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" }, false),
        ListTypeExpressionSyntax l => (new OpenApiSchema { Type = JsonSchemaType.Array, Items = MapType(l.ElementType).Schema }, false),
        OptionalTypeExpressionSyntax o => (MapType(o.ElementType).Schema, true),
        EnumTypeExpressionSyntax e => (EnumSchema(e.Values), false),
        _ => (new OpenApiSchema(), false)
    };

    private static OpenApiSchema MapScalar(ScalarTypeKind kind) => kind switch
    {
        ScalarTypeKind.String => new() { Type = JsonSchemaType.String },
        ScalarTypeKind.Integer => new() { Type = JsonSchemaType.Integer },
        ScalarTypeKind.Number => new() { Type = JsonSchemaType.Number },
        ScalarTypeKind.Boolean => new() { Type = JsonSchemaType.Boolean },
        ScalarTypeKind.Date => new() { Type = JsonSchemaType.String, Format = "date" },
        ScalarTypeKind.DateTime => new() { Type = JsonSchemaType.String, Format = "date-time" },
        ScalarTypeKind.Time => new() { Type = JsonSchemaType.String, Format = "time" },
        ScalarTypeKind.Money => MoneySchema(),
        ScalarTypeKind.Email => new() { Type = JsonSchemaType.String, Format = "email" },
        ScalarTypeKind.Url => new() { Type = JsonSchemaType.String, Format = "uri" },
        ScalarTypeKind.Uuid => new() { Type = JsonSchemaType.String, Format = "uuid" },
        _ => new() { Type = JsonSchemaType.String }
    };

    private static OpenApiSchema MoneySchema() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["amount"] = new OpenApiSchema { Type = JsonSchemaType.Number },
            ["currency"] = new OpenApiSchema { Type = JsonSchemaType.String }
        },
        Required = new HashSet<string>(StringComparer.Ordinal) { "amount", "currency" }
    };

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
            return new LifecycleInfo(HasLifecycle: true, StatesKnown: false, [], [], NonDeterministic: false);
        }

        var states = inline.Members.OfType<StatesBlockSyntax>()
            .SelectMany(b => b.States).Select(s => s.Name)
            .Distinct(StringComparer.Ordinal).ToList();

        var transitions = inline.Members.OfType<TransitionsBlockSyntax>()
            .SelectMany(b => b.Transitions).ToList();

        var transitionNames = transitions.Select(t => t.Name).Distinct(StringComparer.Ordinal).ToList();

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

    private static string? UrlHint(IEnumerable<UrlHintSyntax> hints) =>
        hints.Select(h => h.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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
