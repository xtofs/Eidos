namespace Eidos.Sample.HumanResources;

/// <summary>
/// Zero-dependency in-memory repository, seeded with the same sample data as the SQLite version.
/// Selected via <c>Hr:Provider = InMemory</c>. State resets on every run.
/// </summary>
/// <remarks>
/// Kept deliberately alongside <see cref="SqliteHumanResourcesRepository"/>, even though an
/// in-memory SQLite DSN (e.g. <c>Data Source=hr;Mode=Memory;Cache=Shared</c>) also gives ephemeral
/// storage. Two reasons: it is the only path that needs neither the Microsoft.Data.Sqlite package
/// nor the native SQLite library, and having two genuinely different backends behind one interface
/// is what demonstrates that <see cref="IHumanResourcesRepository"/> is truly storage-agnostic.
/// </remarks>
internal sealed class InMemoryHumanResourcesRepository : IHumanResourcesRepository
{
    private readonly Dictionary<string, PersonDto> _people = new(StringComparer.Ordinal)
    {
        ["ada"] = new("ada", "Ada Lovelace", "ada@example.com", "Active"),
        ["alan"] = new("alan", "Alan Turing", "alan@example.com", "Active")
    };

    private readonly Dictionary<string, EmploymentDto> _employments = new(StringComparer.Ordinal)
    {
        ["employment-1"] = new("employment-1", "ada", "alan", "Research Engineer", "Active")
    };

    public void Initialize()
    {
        // Nothing to do — seed data lives in the field initializers above.
    }

    public Task<IReadOnlyList<PersonDto>> ListPeopleAsync()
        => Task.FromResult<IReadOnlyList<PersonDto>>(_people.Values.ToList());

    public Task<PersonDto?> GetPersonAsync(string key)
        => Task.FromResult(_people.TryGetValue(key, out var person) ? person : null);

    public Task<bool> PersonExistsAsync(string key)
        => Task.FromResult(_people.ContainsKey(key));

    public Task<bool> TryAddPersonAsync(PersonDto person)
    {
        if (_people.ContainsKey(person.Key))
        {
            return Task.FromResult(false);
        }

        _people[person.Key] = person;
        return Task.FromResult(true);
    }

    public Task SavePersonAsync(PersonDto person)
    {
        _people[person.Key] = person;
        return Task.CompletedTask;
    }

    public Task<bool> RemovePersonAsync(string key)
        => Task.FromResult(_people.Remove(key));

    public Task<IReadOnlyList<EmploymentDto>> ListEmploymentsAsync()
        => Task.FromResult<IReadOnlyList<EmploymentDto>>(_employments.Values.ToList());

    public Task<IReadOnlyList<EmploymentDto>> ListEmploymentsByParticipantAsync(string personKey)
    {
        var scoped = _employments.Values
            .Where(e => string.Equals(e.EmployeeKey, personKey, StringComparison.Ordinal)
                || string.Equals(e.EmployerKey, personKey, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<EmploymentDto>>(scoped);
    }

    public Task<EmploymentDto?> GetEmploymentAsync(string key)
        => Task.FromResult(_employments.TryGetValue(key, out var employment) ? employment : null);

    public Task<bool> TryAddEmploymentAsync(EmploymentDto employment)
    {
        if (_employments.ContainsKey(employment.Key))
        {
            return Task.FromResult(false);
        }

        _employments[employment.Key] = employment;
        return Task.FromResult(true);
    }

    public Task SaveEmploymentAsync(EmploymentDto employment)
    {
        _employments[employment.Key] = employment;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveEmploymentAsync(string key)
        => Task.FromResult(_employments.Remove(key));
}
