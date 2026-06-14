using Eidos.AspNetCore;
using Eidos.Core;

namespace Eidos.Sample.HumanResources;

/// <summary>
/// DI + endpoint wiring for the HR sample. The repository implementation is chosen by configuration:
/// <c>Hr:Provider</c> = <c>Sqlite</c> (default) or <c>InMemory</c>. For SQLite the connection string
/// is <c>ConnectionStrings:HrDb</c> (default <c>Data Source=hr.db</c>); set it to a shared in-memory
/// DSN (e.g. <c>Data Source=hr;Mode=Memory;Cache=Shared</c>) for an ephemeral SQLite run.
/// </summary>
public static class HumanResourcesServiceCollectionExtensions
{

    public static WebApplicationBuilder AddHumanResourcesService(this WebApplicationBuilder builder)
    {
        var (services, configuration) = (builder.Services, builder.Configuration);

        var provider = configuration["Hr:Provider"];

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IHumanResourcesRepository, InMemoryHumanResourcesRepository>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("HrDb") ?? "Data Source=hr.db";
            services.AddSingleton<IHumanResourcesRepository>(_ => new SqliteHumanResourcesRepository(connectionString));
        }
        services.AddSingleton(HumanResourcesService.ParsedSchema);

        services.AddSingleton<HumanResourcesService>();

        // add the Eidos OpenAPI document generator and UI using the schema registered in DI
        builder.AddEidosOpenApi(builder.Configuration.GetSection("Eidos:OpenApi"));
        builder.AddEidosMetadata(builder.Configuration.GetSection("Eidos"));

        return builder;
    }

    public static IEndpointRouteBuilder MapHumanResourcesEndpoints(this WebApplication app)
    {

        // redirect root to the configured documentation UI path for a friendlier default landing page
        var uiPath = app.Services.GetRequiredService<EidosOpenApiRouteOptions>().UiPath;
        app.MapGet("/", () => Results.Redirect(uiPath));

        var svc = app.Services.GetRequiredService<HumanResourcesService>();
        svc.MapEndpoints(app);

        return app;
    }
}
