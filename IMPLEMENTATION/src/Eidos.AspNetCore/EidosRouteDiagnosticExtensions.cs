using Microsoft.Extensions.Logging;

namespace Eidos.AspNetCore;

public static class EidosRouteDiagnosticExtensions
{
    public static LogLevel ToLogLevel(this EidosRouteDiagnosticSeverity severity) => severity switch
    {
        EidosRouteDiagnosticSeverity.Error => LogLevel.Error,
        EidosRouteDiagnosticSeverity.Warning => LogLevel.Warning,
        _ => LogLevel.Debug
    };

    public static LogLevel ToLogLevel(this EidosRouteDiagnostic diagnostic) => diagnostic.Severity.ToLogLevel();

    public static void LogDiagnostic(this ILogger logger, EidosRouteDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(diagnostic);

        logger.Log(
            diagnostic.ToLogLevel(),
            "Eidos mapping {Severity}: {Message}",
            diagnostic.Severity,
            diagnostic.Message);
    }
}
