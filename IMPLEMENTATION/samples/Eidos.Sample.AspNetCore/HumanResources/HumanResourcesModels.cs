namespace Eidos.Sample.HumanResources;

public sealed record PersonDto(string Key, string Name, string Email, string State);

public sealed record EmploymentDto(string Key, string EmployeeKey, string EmployerKey, string Title, string State);

public sealed record PersonCreateRequest(string? Key, string Name, string Email);

public sealed record PersonPatchRequest(string? Name, string? Email);

public sealed record EmploymentCreateRequest(string? Key, string EmployeeKey, string EmployerKey, string Title);

public sealed record EmploymentPatchRequest(string? Title);
