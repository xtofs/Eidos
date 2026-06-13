# Eidos.Sample.AspNetCore

Minimal ASP.NET Core app that demonstrates Eidos-driven endpoint mapping using the fluent API from Eidos.AspNetCore.

## What it shows

- Parse Eidos schema text into AST with EidosGrammarParser.
- Register Entity and Relationship routes with fluent mapping.
- Use V0.1 key semantics (`key : String`) via `/persons/{key}` and `/employments/{key}`.
- Emit diagnostics from route coverage validation during startup.

## Run

```bash
dotnet run --project samples/Eidos.Sample.AspNetCore
```

## Try

```bash
curl http://localhost:5000/
curl http://localhost:5000/persons
curl http://localhost:5000/employments

curl -X POST http://localhost:5000/persons \
  -H "Content-Type: application/json" \
  -d '{"key":"person-grace","name":"Grace Hopper","email":"grace@example.com"}'

curl http://localhost:5000/persons/person-grace
```
