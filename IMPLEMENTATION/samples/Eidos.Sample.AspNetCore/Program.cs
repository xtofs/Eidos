using Eidos.AspNetCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // builder.Services.AddHttpLogging(logging =>
        // {
        //     logging.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath | HttpLoggingFields.RequestScheme | HttpLoggingFields.RequestQuery |
        //                             HttpLoggingFields.ResponseStatusCode;
        // });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Logging.AddConsole();


        var app = builder.Build();
        app.Use(SimpleHttpLoggingMiddleware);

        // wire up the management API
        app.MapEndpoints();

        app.Run();

    }

    private static async Task SimpleHttpLoggingMiddleware(HttpContext context, Func<Task> next)
    {
        var sw = Stopwatch.StartNew();
        await next();
        sw.Stop();

        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RequestSummary");

        logger.LogInformation(
            "{method} {path}{query} => {status} {reason} in {elapsed}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Response.StatusCode,
            ReasonPhrases.GetReasonPhrase(context.Response.StatusCode),
            sw.ElapsedMilliseconds);
    }
}

