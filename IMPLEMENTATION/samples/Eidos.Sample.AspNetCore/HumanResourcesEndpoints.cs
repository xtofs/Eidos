using Eidos.AspNetCore;
using Eidos.Parser;



public static class HumanResourcesEndpoints
{

    // TODO: in a real app, this would likely be an extension method on WebApplicationBuilder that registers the repository and service types with the DI container, and then an extension method on WebApplication that maps the endpoints. For simplicity of the sample, we're just doing it all in one extension method here.
    // TODO: this should be set up via DI and not directly reference the repository from the service, but this is just a sample
    public static IEndpointRouteBuilder MapEndpoints(this WebApplication app)
    {
        var repository = new HumanResourcesRepository();
        var api = new HumanResourcesService(repository);
        api.MapEndpoints(app);

        return app;
    }
}



internal class HumanResourcesRepository
{
    private readonly Dictionary<string, PersonDto> people = new Dictionary<string, PersonDto>(StringComparer.Ordinal)
    {
        ["person-ada"] = new("person-ada", "Ada Lovelace", "ada@example.com", "Active"),
        ["person-alan"] = new("person-alan", "Alan Turing", "alan@example.com", "Active")
    };

    public IDictionary<string, PersonDto> People => people;

    private readonly Dictionary<string, EmploymentDto> employments = new Dictionary<string, EmploymentDto>(StringComparer.Ordinal)
    {
        ["employment-1"] = new("employment-1", "person-ada", "person-alan", "Research Engineer", "Active")
    };

    public IDictionary<string, EmploymentDto> Employments => employments;


}


internal class HumanResourcesService(HumanResourcesRepository repository)
{


    const string SCHEMA = """
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

        var schema = EidosGrammarParser.Parse(SCHEMA);

        app.MapEidos(schema, map =>
        {
            // (OrdersEndpointsImpl impl) => impl.GetAll());

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

    static LogLevel MapSeverity(EidosRouteDiagnosticSeverity severity) => severity switch
    {
        EidosRouteDiagnosticSeverity.Error => LogLevel.Error,
        EidosRouteDiagnosticSeverity.Warning => LogLevel.Warning,
        _ => LogLevel.Debug
    };

    IResult ListPeople()
    {
        return Results.Ok(repository.People.Values);
    }

    IResult GetPersonEntity(string key)
    {
        return repository.People.TryGetValue(key, out var person) ? Results.Ok(person) : Results.NotFound();
    }

    IResult CreatePerson(PersonCreateRequest request)
    {
        var key = string.IsNullOrWhiteSpace(request.Key)
            ? $"person-{Guid.NewGuid():N}"[..15]
            : request.Key;

        if (repository.People.ContainsKey(key))
        {
            return Results.Conflict(new { message = $"Person with key '{key}' already exists." });
        }

        var person = new PersonDto(key, request.Name, request.Email, "Active");
        repository.People[key] = person;
        return Results.Created($"/persons/{key}", person);
    }

    IResult TransitionPerson(string key, StateTransitionRequest request)
    {
        if (!repository.People.TryGetValue(key, out var existing))
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            State = request.State
        };

        repository.People[key] = updated;
        return Results.Ok(updated);
    }

    IResult UpdatePerson(string key, PersonPatchRequest request)
    {
        if (!repository.People.TryGetValue(key, out var existing))
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            Email = request.Email ?? existing.Email
        };

        repository.People[key] = updated;
        return Results.Ok(updated);
    }

    IResult DeletePerson(string key)
    {
        if (!repository.People.Remove(key))
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }

    IResult ListEmployments()
    {
        return Results.Ok(repository.Employments.Values);
    }

    IResult ListEmploymentsByParticipant(string participantTypeName, string key)
    {
        if (!string.Equals(participantTypeName, "Person", StringComparison.Ordinal))
        {
            return Results.Ok(Array.Empty<EmploymentDto>());
        }

        var scoped = repository.Employments.Values
            .Where(e => string.Equals(e.EmployeeKey, key, StringComparison.Ordinal)
                || string.Equals(e.EmployerKey, key, StringComparison.Ordinal))
            .ToArray();

        return Results.Ok(scoped);
    }

    IResult GetEmploymentEntity(string key)
    {
        if (!repository.Employments.TryGetValue(key, out var employment))
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

    object? ResolvePersonForExpand(string key)
    {
        return repository.People.TryGetValue(key, out var person) ? person : null;
    }

    IResult CreateEmployment(EmploymentCreateRequest request)
    {
        if (!repository.People.ContainsKey(request.EmployeeKey) || !repository.People.ContainsKey(request.EmployerKey))
        {
            return Results.BadRequest(new { message = "employeeKey and employerKey must reference existing persons." });
        }

        var key = string.IsNullOrWhiteSpace(request.Key)
            ? $"employment-{Guid.NewGuid():N}"[..19]
            : request.Key;

        if (repository.Employments.ContainsKey(key))
        {
            return Results.Conflict(new { message = $"Employment with key '{key}' already exists." });
        }

        var employment = new EmploymentDto(key, request.EmployeeKey, request.EmployerKey, request.Title, "Active");
        repository.Employments[key] = employment;
        return Results.Created($"/employments/{key}", employment);
    }

    IResult TransitionEmployment(string key, StateTransitionRequest request)
    {
        if (!repository.Employments.TryGetValue(key, out var existing))
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            State = request.State
        };

        repository.Employments[key] = updated;
        return Results.Ok(updated);
    }

    IResult UpdateEmployment(string key, EmploymentPatchRequest request)
    {
        if (!repository.Employments.TryGetValue(key, out var existing))
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            Title = request.Title ?? existing.Title
        };

        repository.Employments[key] = updated;
        return Results.Ok(updated);
    }

    IResult DeleteEmployment(string key)
    {
        if (!repository.Employments.Remove(key))
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }


}

public sealed record PersonDto(string Key, string Name, string Email, string State);

public sealed record EmploymentDto(string Key, string EmployeeKey, string EmployerKey, string Title, string State);

public sealed record PersonCreateRequest(string? Key, string Name, string Email);

public sealed record PersonPatchRequest(string? Name, string? Email);

public sealed record EmploymentCreateRequest(string? Key, string EmployeeKey, string EmployerKey, string Title);

public sealed record EmploymentPatchRequest(string? Title);
