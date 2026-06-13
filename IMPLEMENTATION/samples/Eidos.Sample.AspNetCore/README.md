# Eidos.Sample.AspNetCore

Minimal ASP.NET Core app that demonstrates Eidos-driven endpoint mapping using the fluent API from Eidos.AspNetCore.

## What it shows

- Parse Eidos schema text into AST with EidosGrammarParser.
- Register Entity and Relationship routes with fluent mapping.
- Use V0.1 key semantics (`key : String`) via `/persons/{key}` and `/employments/{key}`.
- Emit diagnostics from route coverage validation during startup.
- Back the handlers with a real repository (`IHumanResourcesRepository`) — SQLite or in-memory.

## Storage

Handlers are storage-agnostic; the repository is chosen by configuration (`appsettings.json`):

| Setting                  | Default             | Effect                                                                 |
| ------------------------ | ------------------- | ---------------------------------------------------------------------- |
| `Hr:Provider`            | `Sqlite`            | `Sqlite` or `InMemory` (dependency-free dictionaries, resets each run) |
| `ConnectionStrings:HrDb` | `Data Source=hr.db` | SQLite connection string. Use a file for persistence…                  |

The schema is created and seeded (Ada, Alan, employment-1) on first startup. With the default file
provider, data **persists across restarts**. For an ephemeral SQLite database, set the connection string to a
shared in-memory DSN:

```bash
ConnectionStrings__HrDb="Data Source=hr;Mode=Memory;Cache=Shared" \
  dotnet run --project samples/Eidos.Sample.AspNetCore
```

(`hr.db` is git-ignored.)

## Run

```bash
dotnet run --project samples/Eidos.Sample.AspNetCore   # http://localhost:5135
```

## Try

```bash
curl http://localhost:5135/
curl http://localhost:5135/persons
curl http://localhost:5135/employments
curl "http://localhost:5135/employments/employment-1?expand=employee,employer"

curl -X POST http://localhost:5135/persons \
  -H "Content-Type: application/json" \
  -d '{"key":"grace","name":"Grace Hopper","email":"grace@example.com"}'

curl http://localhost:5135/persons/grace

curl -X PUT http://localhost:5135/persons/grace/_state \
  -H "Content-Type: application/json" \
  -d '{"state":"Inactive"}'
```
