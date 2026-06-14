using System.Linq;
using System.Net.Http;
using Eidos.Core.OpenApi;
using Microsoft.OpenApi;

namespace Eidos.Core.Tests;

public class OpenApiDocumentGeneratorTests
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

    private static OpenApiDocument Generate() =>
        OpenApiDocumentGenerator.Generate(EidosGrammarParser.Parse(Schema), new ApiInfo("Test", "1.0"));

    private static OpenApiOperation Operation(OpenApiDocument doc, string template, HttpMethod method) =>
        ((OpenApiPathItem)doc.Paths[template]).Operations![method];

    private static OpenApiSchema AsSchema(IOpenApiSchema schema) => Assert.IsType<OpenApiSchema>(schema);

    private static IEnumerable<string> EnumValues(IOpenApiSchema schema) =>
        AsSchema(schema).Enum!.Select(n => n!.GetValue<string>());

    [Fact]
    public void EmitsComponentsForEntitiesRelationshipsAndJsonPatch()
    {
        var doc = Generate();
        Assert.Contains("Person", doc.Components!.Schemas!.Keys);
        Assert.Contains("Employment", doc.Components.Schemas.Keys);
        Assert.Contains("JsonPatch", doc.Components.Schemas.Keys);
    }

    [Fact]
    public void EntitySchemaMapsScalarsAndReservedFields()
    {
        var doc = Generate();
        var person = AsSchema(doc.Components!.Schemas!["Person"]);

        var email = AsSchema(person.Properties!["email"]);
        Assert.Equal(JsonSchemaType.String, email.Type);
        Assert.Equal("email", email.Format);
        Assert.Contains("email", person.Required!);

        Assert.True(AsSchema(person.Properties["_self"]).ReadOnly);
        Assert.True(AsSchema(person.Properties["_type"]).ReadOnly);
        Assert.True(AsSchema(person.Properties["_state"]).ReadOnly); // Person has a lifecycle
    }

    [Fact]
    public void EmitsCrudAndStatePaths()
    {
        var doc = Generate();
        Assert.Contains("/persons", doc.Paths.Keys);
        Assert.Contains("/persons/{key}", doc.Paths.Keys);
        Assert.Contains("/persons/{key}/_state", doc.Paths.Keys);
    }

    [Fact]
    public void StateBodyEnumeratesArchetypeStates()
    {
        var doc = Generate();
        var put = Operation(doc, "/persons/{key}/_state", HttpMethod.Put);

        var body = AsSchema(put.RequestBody!.Content!["application/json"].Schema!);
        Assert.Equal(new[] { "Inactive", "Active" }, EnumValues(body.Properties!["state"]));

        // Activatable is deterministic, so no 'transition' field.
        Assert.DoesNotContain("transition", body.Properties!.Keys);
    }

    [Fact]
    public void PatchUsesJsonPatchMediaTypeAndComponentReference()
    {
        var doc = Generate();
        var patch = Operation(doc, "/persons/{key}", HttpMethod.Patch);

        var content = patch.RequestBody!.Content!;
        Assert.Contains("application/json-patch+json", content.Keys);
        var reference = Assert.IsType<OpenApiSchemaReference>(content["application/json-patch+json"].Schema);
        Assert.Equal("JsonPatch", reference.Reference!.Id);
    }

    [Fact]
    public void RelationshipEmitsExpandParameterAndParticipantProjection()
    {
        var doc = Generate();

        var get = Operation(doc, "/employments/{key}", HttpMethod.Get);
        var expand = get.Parameters!.Single(p => p.Name == "expand");
        Assert.Equal(ParameterLocation.Query, expand.In);
        Assert.Equal(new[] { "employee", "employer" }, EnumValues(expand.Schema!));

        Assert.Contains("/persons/{key}/employments", doc.Paths.Keys);
    }

    [Fact]
    public void UrlHintOverridesCollectionSegmentEverywhere()
    {
        var doc = OpenApiDocumentGenerator.Generate(EidosGrammarParser.Parse("""
            entity Person {
              url: "people"
              lifecycle: Activatable
            }

            relationship Employment between employee : Person, employer : Person {
            }
            """), new ApiInfo("Test", "1.0"));

        // Own routes use the hinted segment...
        Assert.Contains("/people", doc.Paths.Keys);
        Assert.Contains("/people/{key}", doc.Paths.Keys);
        Assert.DoesNotContain("/persons", doc.Paths.Keys);

        // ...and so does the participant projection that anchors on Person.
        Assert.Contains("/people/{key}/employments", doc.Paths.Keys);
    }
}
