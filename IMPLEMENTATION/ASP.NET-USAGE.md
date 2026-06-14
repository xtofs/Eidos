# Eidos ASP.NET Usage

This guide covers the ASP.NET Core integration only.

## 1. Register the schema and docs

Bind the OpenAPI configuration from appsettings and register your schema once in DI.

```csharp
builder.Services.AddSingleton(MySchemaProvider.Schema);
builder.AddEidosOpenApi(builder.Configuration.GetSection("Eidos:OpenApi"));
builder.AddEidosMetadata(builder.Configuration.GetSection("Eidos"));
```

Example configuration:

```json
{
  "Eidos": {
    "MetadataPath": "/routes",
    "OpenApi": {
      "Title": "My API",
      "Version": "1.0",
      "Routes": {
        "OpenApiPath": "/swagger/v1/swagger.json",
        "UiPath": "/redoc"
      }
    }
  }
}
```

## 2. Map handlers

Use the fluent mapping API to register handlers against your schema.

```csharp
app.MapEidosSurface(MySchemaProvider.Schema, map =>
{
    map.Entity("Person", person => person
        .List(ListPeople)
        .Get(GetPerson)
        .Create(CreatePerson));
});
```

`MapEidosSurface` uses the configured metadata, OpenAPI, and ReDoc paths from DI.

## 3. Log diagnostics

Use the convenience logging extension for ASP.NET Core diagnostics.

```csharp
options.OnDiagnostic = diagnostic => app.Logger.LogDiagnostic(diagnostic);
```

If you need the raw diagnostic object, keep using `OnDiagnostic` directly.

## 4. Recommended request flow

1. Register schema and docs in `Program.cs`.
2. Build the app.
3. Map the surface with `MapEidosSurface(...)`.
4. Redirect `/` to `/redoc` (or your UiPath if you have configured it differently) if you want a documentation landing page.
