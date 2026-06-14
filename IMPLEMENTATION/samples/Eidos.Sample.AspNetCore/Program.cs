using System.Text.Json.Serialization;
using Eidos.AspNetCore;
using Eidos.Sample.HumanResources;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // configure JSON options globally to support PATCH requests with JsonPatchDocument<T> payloads, and to serialize enums as strings in OpenAPI docs
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // register the HR service and repository (SQLite by default; see appsettings 'Hr:Provider' / 'ConnectionStrings:HrDb')
        builder.AddHumanResourcesService();

        // ////////////////////////
        // Web Application configuration

        var app = builder.Build();

        // add custom middleware to log HTTP requests and responses with Serilog; in production you'd likely want to use a more robust logging solution (e.g. with rolling file sinks, etc.) and not log at the INFO level
        app.UseMiddleware<HttpRequestSummaryMiddleware>();

        // add the endpoints for the HR service based on the Eidos document
        app.MapHumanResourcesEndpoints();

        // ////////////////////////

        // create schema + seed sample data once at startup
        app.Services.GetRequiredService<IHumanResourcesRepository>().Initialize();

        app.Run();

    }
}
