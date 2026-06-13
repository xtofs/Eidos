using Eidos.AspNetCore;
using Eidos.Parser;

namespace Eidos.Sample.HumanResources;

/// <summary>
/// Maps the HR Eidos schema onto endpoints and implements the handlers against
/// <see cref="IHumanResourcesRepository"/>. Storage-agnostic — the repository is injected.
/// </summary>
internal sealed class HumanResourcesService(IHumanResourcesRepository repository)
{
    private const string Schema = """
        entity Person {
            lifecycle: Activatable
        }

        relationship Employment between employee : Person, employer : Person
        {
            lifecycle: Activatable
        }
        """;

    public void MapEndpoints(WebApplication app)
    {
        var schema = EidosGrammarParser.Parse(Schema);

        app.MapEidos(schema, map =>
        {
            map
                .Entity("Person", p => p
                    .List(ListPeople)
                    .GetEntity(GetPersonEntity)
                    .Create<PersonCreateRequest>(CreatePerson)
                    .Transition(TransitionPerson)
                    .Update<PersonPatchRequest>(UpdatePerson)
                    .Delete(DeletePerson))
                .Relationship("Employment", e => e
                    .List(ListEmployments)
                    .ListByParticipant(ListEmploymentsByParticipant)
                    .GetEntity(GetEmploymentEntity, ResolvePersonForExpand, ResolvePersonForExpand)
                    .Create<EmploymentCreateRequest>(CreateEmployment)
                    .Transition(TransitionEmployment)
                    .Update<EmploymentPatchRequest>(UpdateEmployment)
                    .Delete(DeleteEmployment))
                .MapMetadataEndpoint("/");
        }, options =>
        {
            options.OnDiagnostic = diagnostic =>
                app.Logger.Log(MapSeverity(diagnostic.Severity),
                    "Eidos mapping {Severity}: {Message}",
                    diagnostic.Severity,
                    diagnostic.Message);
        });
    }

    private static LogLevel MapSeverity(EidosRouteDiagnosticSeverity severity) => severity switch
    {
        EidosRouteDiagnosticSeverity.Error => LogLevel.Error,
        EidosRouteDiagnosticSeverity.Warning => LogLevel.Warning,
        _ => LogLevel.Debug
    };

    private async Task<IResult> ListPeople()
        => Results.Ok(await repository.ListPeopleAsync());

    private async Task<IResult> GetPersonEntity(string key)
    {
        var person = await repository.GetPersonAsync(key);
        return person is null ? Results.NotFound() : Results.Ok(person);
    }

    private async Task<IResult> CreatePerson(PersonCreateRequest request)
    {
        var key = string.IsNullOrWhiteSpace(request.Key)
            ? $"person-{Guid.NewGuid():N}"[..15]
            : request.Key;

        var person = new PersonDto(key, request.Name, request.Email, "Active");

        if (!await repository.TryAddPersonAsync(person))
        {
            return Results.Conflict(new { message = $"Person with key '{key}' already exists." });
        }

        return Results.Created($"/persons/{key}", person);
    }

    private async Task<IResult> TransitionPerson(string key, StateTransitionRequest request)
    {
        var existing = await repository.GetPersonAsync(key);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with { State = request.State };
        await repository.SavePersonAsync(updated);
        return Results.Ok(updated);
    }

    private async Task<IResult> UpdatePerson(string key, PersonPatchRequest request)
    {
        var existing = await repository.GetPersonAsync(key);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            Email = request.Email ?? existing.Email
        };

        await repository.SavePersonAsync(updated);
        return Results.Ok(updated);
    }

    private async Task<IResult> DeletePerson(string key)
        => await repository.RemovePersonAsync(key) ? Results.NoContent() : Results.NotFound();

    private async Task<IResult> ListEmployments()
        => Results.Ok(await repository.ListEmploymentsAsync());

    private async Task<IResult> ListEmploymentsByParticipant(string participantTypeName, string key)
    {
        if (!string.Equals(participantTypeName, "Person", StringComparison.Ordinal))
        {
            return Results.Ok(Array.Empty<EmploymentDto>());
        }

        return Results.Ok(await repository.ListEmploymentsByParticipantAsync(key));
    }

    private async Task<IResult> GetEmploymentEntity(string key)
    {
        var employment = await repository.GetEmploymentAsync(key);
        if (employment is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = employment.Key,
            ["employee"] = employment.EmployeeKey,
            ["employer"] = employment.EmployerKey,
            ["title"] = employment.Title,
            ["state"] = employment.State
        });
    }

    private async Task<object?> ResolvePersonForExpand(string key)
        => await repository.GetPersonAsync(key);

    private async Task<IResult> CreateEmployment(EmploymentCreateRequest request)
    {
        if (!await repository.PersonExistsAsync(request.EmployeeKey)
            || !await repository.PersonExistsAsync(request.EmployerKey))
        {
            return Results.BadRequest(new { message = "employeeKey and employerKey must reference existing persons." });
        }

        var key = string.IsNullOrWhiteSpace(request.Key)
            ? $"employment-{Guid.NewGuid():N}"[..19]
            : request.Key;

        var employment = new EmploymentDto(key, request.EmployeeKey, request.EmployerKey, request.Title, "Active");

        if (!await repository.TryAddEmploymentAsync(employment))
        {
            return Results.Conflict(new { message = $"Employment with key '{key}' already exists." });
        }

        return Results.Created($"/employments/{key}", employment);
    }

    private async Task<IResult> TransitionEmployment(string key, StateTransitionRequest request)
    {
        var existing = await repository.GetEmploymentAsync(key);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with { State = request.State };
        await repository.SaveEmploymentAsync(updated);
        return Results.Ok(updated);
    }

    private async Task<IResult> UpdateEmployment(string key, EmploymentPatchRequest request)
    {
        var existing = await repository.GetEmploymentAsync(key);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with { Title = request.Title ?? existing.Title };
        await repository.SaveEmploymentAsync(updated);
        return Results.Ok(updated);
    }

    private async Task<IResult> DeleteEmployment(string key)
        => await repository.RemoveEmploymentAsync(key) ? Results.NoContent() : Results.NotFound();
}
