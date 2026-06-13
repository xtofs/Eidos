namespace Eidos.Sample.HumanResources;

/// <summary>
/// Storage contract for the HR sample. Intentionally storage-agnostic — see the in-memory and
/// SQLite implementations. All reads/writes are async because the SQLite implementation does I/O.
/// </summary>
public interface IHumanResourcesRepository
{
    /// <summary>Create any backing schema and seed sample data. Called once at startup.</summary>
    void Initialize();

    // People
    Task<IReadOnlyList<PersonDto>> ListPeopleAsync();
    Task<PersonDto?> GetPersonAsync(string key);
    Task<bool> PersonExistsAsync(string key);

    /// <summary>Insert a new person. Returns false if the key already exists (no change made).</summary>
    Task<bool> TryAddPersonAsync(PersonDto person);

    /// <summary>Persist changes to an existing person (used by property PATCH and state transitions).</summary>
    Task SavePersonAsync(PersonDto person);

    /// <summary>Delete a person. Returns false if no such key existed.</summary>
    Task<bool> RemovePersonAsync(string key);

    // Employment relationships between people. 
    // This is a binary relationships with "employee" and "employer" roles. In a more complex domain you might have n-ary relationships and/or more flexible role definitions.

    // Employments
    Task<IReadOnlyList<EmploymentDto>> ListEmploymentsAsync();

    /// <summary>Employments where the person plays either participant (employee or employer).</summary>
    Task<IReadOnlyList<EmploymentDto>> ListEmploymentsByParticipantAsync(string personKey);

    Task<EmploymentDto?> GetEmploymentAsync(string key);
    Task<bool> TryAddEmploymentAsync(EmploymentDto employment);
    Task SaveEmploymentAsync(EmploymentDto employment);
    Task<bool> RemoveEmploymentAsync(string key);
}
