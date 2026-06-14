using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;


public class HttpRequestSummaryMiddleware(RequestDelegate next, ILogger<HttpRequestSummaryMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<HttpRequestSummaryMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        // HTTP GET /persons/chris responded 200(OK) in 6.5010 ms
        _logger.LogInformation(
            "HTTP {method} {path}{query} responded {status}({reason}) in {elapsed}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Response.StatusCode,
            ReasonPhrases.GetReasonPhrase(context.Response.StatusCode),
            sw.ElapsedMilliseconds);
    }
}