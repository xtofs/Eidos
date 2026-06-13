using System.Linq;
using Eidos.Core.OpenApi;

namespace Eidos.Core.Tests;

public class OpenApiModelGeneratorTests
{
    private const string Schema = """
        archetype Activatable composable {
          initial: Inactive
          states { Inactive Active }
          transitions {
            activate   : Inactive -> Active
            deactivate : Active   -> Inactive
          }
        }

        entity Person {
          properties {
            name  : String [ required ]
            email : Email  [ required ]
          }
          lifecycle: Activatable
        }

        relationship Employment between employee : Person, employer : Person {
          lifecycle: Activatable
        }
        """;

    private static OpenApiModel Generate() =>
        OpenApiModelGenerator.Generate(EidosGrammarParser.Parse(Schema), new ApiInfo("Test", "1.0"));

    private static ApiOperation Operation(OpenApiModel model, string template, ApiMethod method) =>
        model.Paths.Single(p => p.Template == template).Operations.Single(o => o.Method == method);

    [Fact]
    public void EmitsComponentsForEntitiesRelationshipsAndJsonPatch()
    {
        var model = Generate();
        var names = model.Components.Select(c => c.Name).ToHashSet();

        Assert.Contains("Person", names);
        Assert.Contains("Employment", names);
        Assert.Contains("JsonPatch", names);
    }

    [Fact]
    public void EntitySchemaMapsScalarsAndReservedFields()
    {
        var model = Generate();
        var person = Assert.IsType<ApiObjectSchema>(model.Components.Single(c => c.Name == "Person").Schema);

        var email = person.Properties.Single(p => p.Name == "email");
        var scalar = Assert.IsType<ApiScalarSchema>(email.Schema);
        Assert.Equal(ApiScalarType.String, scalar.Type);
        Assert.Equal("email", scalar.Format);
        Assert.True(email.Required);

        // Reserved fields are appended (and _state because Person has a lifecycle).
        Assert.Contains(person.Properties, p => p.Name == "_self" && p.ReadOnly);
        Assert.Contains(person.Properties, p => p.Name == "_type" && p.ReadOnly);
        Assert.Contains(person.Properties, p => p.Name == "_state" && p.ReadOnly);
    }

    [Fact]
    public void EmitsCrudAndStatePaths()
    {
        var model = Generate();
        var templates = model.Paths.Select(p => p.Template).ToHashSet();

        Assert.Contains("/persons", templates);
        Assert.Contains("/persons/{key}", templates);
        Assert.Contains("/persons/{key}/_state", templates);
    }

    [Fact]
    public void StateBodyEnumeratesArchetypeStates()
    {
        var model = Generate();
        var put = Operation(model, "/persons/{key}/_state", ApiMethod.Put);

        var body = Assert.IsType<ApiObjectSchema>(put.RequestBody!.Schema);
        var state = body.Properties.Single(p => p.Name == "state");
        var stateEnum = Assert.IsType<ApiEnumSchema>(state.Schema);
        Assert.Equal(new[] { "Inactive", "Active" }, stateEnum.Values);

        // Activatable is deterministic, so no 'transition' field is required.
        Assert.DoesNotContain(body.Properties, p => p.Name == "transition");
    }

    [Fact]
    public void PatchUsesJsonPatchMediaTypeAndComponent()
    {
        var model = Generate();
        var patch = Operation(model, "/persons/{key}", ApiMethod.Patch);

        Assert.Equal("application/json-patch+json", patch.RequestBody!.MediaType);
        Assert.Equal("JsonPatch", Assert.IsType<ApiReferenceSchema>(patch.RequestBody.Schema).ComponentName);
    }

    [Fact]
    public void RelationshipEmitsExpandParameterAndParticipantProjection()
    {
        var model = Generate();

        // ?expand on the canonical GET, enumerating participant names.
        var get = Operation(model, "/employments/{key}", ApiMethod.Get);
        var expand = get.Parameters.Single(p => p.Name == "expand");
        Assert.Equal(ApiParameterLocation.Query, expand.In);
        Assert.Equal(new[] { "employee", "employer" }, Assert.IsType<ApiEnumSchema>(expand.Schema).Values);

        // Participant projection path (both participants are Person → deduped to one).
        Assert.Contains(model.Paths, p => p.Template == "/persons/{key}/employments");
    }
}
