using Microsoft.Data.Sqlite;

namespace Eidos.Sample.HumanResources;

/// <summary>
/// SQLite-backed repository using raw <see cref="Microsoft.Data.Sqlite"/> (parameterized SQL).
/// Holds one connection open for the app lifetime and serializes access with a semaphore — simple
/// and correct for both a file database and a shared in-memory one (a per-request connection would
/// drop a <c>:memory:</c> database between calls). Selected via <c>Hr:Provider = Sqlite</c> (default);
/// the connection string comes from <c>ConnectionStrings:HrDb</c> (default <c>Data Source=hr.db</c>).
/// </summary>
internal sealed class SqliteHumanResourcesRepository : IHumanResourcesRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    public SqliteHumanResourcesRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        Execute("PRAGMA foreign_keys = ON;");
        Execute("""
            CREATE TABLE IF NOT EXISTS people (
                key   TEXT PRIMARY KEY,
                name  TEXT NOT NULL,
                email TEXT NOT NULL,
                state TEXT NOT NULL
            );
            """);
        Execute("""
            CREATE TABLE IF NOT EXISTS employments (
                key          TEXT PRIMARY KEY,
                employee_key TEXT NOT NULL REFERENCES people(key),
                employer_key TEXT NOT NULL REFERENCES people(key),
                title        TEXT,
                state        TEXT NOT NULL
            );
            """);

        SeedIfEmpty();
    }

    public Task<IReadOnlyList<PersonDto>> ListPeopleAsync()
        => WithConnectionAsync<IReadOnlyList<PersonDto>>(async conn =>
        {
            var list = new List<PersonDto>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, name, email, state FROM people ORDER BY key;";
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(ReadPerson(reader));
            }

            return list;
        });

    public Task<PersonDto?> GetPersonAsync(string key)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, name, email, state FROM people WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            return await reader.ReadAsync().ConfigureAwait(false) ? ReadPerson(reader) : null;
        });

    public Task<bool> PersonExistsAsync(string key)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM people WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteScalarAsync().ConfigureAwait(false) is not null;
        });

    public Task<bool> TryAddPersonAsync(PersonDto person)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT OR IGNORE INTO people (key, name, email, state) VALUES ($key, $name, $email, $state);";
            AddPersonParameters(cmd, person);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        });

    public Task SavePersonAsync(PersonDto person)
        => WithConnectionAsync<object?>(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE people SET name = $name, email = $email, state = $state WHERE key = $key;";
            AddPersonParameters(cmd, person);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return null;
        });

    public Task<bool> RemovePersonAsync(string key)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM people WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        });

    public Task<IReadOnlyList<EmploymentDto>> ListEmploymentsAsync()
        => WithConnectionAsync<IReadOnlyList<EmploymentDto>>(async conn =>
        {
            var list = new List<EmploymentDto>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT key, employee_key, employer_key, title, state FROM employments ORDER BY key;";
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(ReadEmployment(reader));
            }

            return list;
        });

    public Task<IReadOnlyList<EmploymentDto>> ListEmploymentsByParticipantAsync(string personKey)
        => WithConnectionAsync<IReadOnlyList<EmploymentDto>>(async conn =>
        {
            var list = new List<EmploymentDto>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT key, employee_key, employer_key, title, state
                FROM employments
                WHERE employee_key = $key OR employer_key = $key
                ORDER BY key;
                """;
            cmd.Parameters.AddWithValue("$key", personKey);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(ReadEmployment(reader));
            }

            return list;
        });

    public Task<EmploymentDto?> GetEmploymentAsync(string key)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT key, employee_key, employer_key, title, state FROM employments WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            return await reader.ReadAsync().ConfigureAwait(false) ? ReadEmployment(reader) : null;
        });

    public Task<bool> TryAddEmploymentAsync(EmploymentDto employment)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO employments (key, employee_key, employer_key, title, state)
                VALUES ($key, $employee, $employer, $title, $state);
                """;
            AddEmploymentParameters(cmd, employment);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        });

    public Task SaveEmploymentAsync(EmploymentDto employment)
        => WithConnectionAsync<object?>(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE employments
                SET employee_key = $employee, employer_key = $employer, title = $title, state = $state
                WHERE key = $key;
                """;
            AddEmploymentParameters(cmd, employment);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return null;
        });

    public Task<bool> RemoveEmploymentAsync(string key)
        => WithConnectionAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM employments WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        });

    public void Dispose()
    {
        _connection?.Dispose();
        _gate.Dispose();
    }

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, Task<T>> action)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Repository not initialized; call Initialize() first.");
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action(_connection).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Execute(string sql)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void SeedIfEmpty()
    {
        using (var count = _connection!.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM people;";
            if (Convert.ToInt64(count.ExecuteScalar()) > 0)
            {
                return;
            }
        }

        InsertSeedPerson(new PersonDto("ada", "Ada Lovelace", "ada@example.com", "Active"));
        InsertSeedPerson(new PersonDto("alan", "Alan Turing", "alan@example.com", "Active"));

        using var employment = _connection!.CreateCommand();
        employment.CommandText = """
            INSERT INTO employments (key, employee_key, employer_key, title, state)
            VALUES ($key, $employee, $employer, $title, $state);
            """;
        AddEmploymentParameters(employment, new EmploymentDto(
            "employment-1", "ada", "alan", "Research Engineer", "Active"));
        employment.ExecuteNonQuery();
    }

    private void InsertSeedPerson(PersonDto person)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "INSERT INTO people (key, name, email, state) VALUES ($key, $name, $email, $state);";
        AddPersonParameters(cmd, person);
        cmd.ExecuteNonQuery();
    }

    private static void AddPersonParameters(SqliteCommand cmd, PersonDto person)
    {
        cmd.Parameters.AddWithValue("$key", person.Key);
        cmd.Parameters.AddWithValue("$name", person.Name);
        cmd.Parameters.AddWithValue("$email", person.Email);
        cmd.Parameters.AddWithValue("$state", person.State);
    }

    private static void AddEmploymentParameters(SqliteCommand cmd, EmploymentDto employment)
    {
        cmd.Parameters.AddWithValue("$key", employment.Key);
        cmd.Parameters.AddWithValue("$employee", employment.EmployeeKey);
        cmd.Parameters.AddWithValue("$employer", employment.EmployerKey);
        cmd.Parameters.AddWithValue("$title", (object?)employment.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$state", employment.State);
    }

    private static PersonDto ReadPerson(SqliteDataReader reader)
        => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));

    private static EmploymentDto ReadEmployment(SqliteDataReader reader)
        => new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            reader.GetString(4));
}
