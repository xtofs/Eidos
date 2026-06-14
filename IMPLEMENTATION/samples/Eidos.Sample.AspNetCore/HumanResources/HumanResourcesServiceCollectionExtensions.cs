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
        AddHumanResourcesService(builder.Services, builder.Configuration);
        return builder;
    }

    public static void AddHumanResourcesService(this IServiceCollection services, IConfiguration configuration)
    {

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
    }

    public static IEndpointRouteBuilder MapHrEndpoints(this WebApplication app)
    {
        var repository = app.Services.GetRequiredService<IHumanResourcesRepository>();
        new HumanResourcesService(repository).MapEndpoints(app);
        return app;
    }
}
