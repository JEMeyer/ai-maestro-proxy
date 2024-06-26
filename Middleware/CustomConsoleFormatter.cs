using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace ai_maestro_proxy.Middleware
{
    public class CustomConsoleFormatter : ConsoleFormatter
    {
        public CustomConsoleFormatter() : base("custom") { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var logLevel = logEntry.LogLevel;
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            var logLevelString = GetLogLevelString(logLevel);
            var logLevelColor = GetLogLevelColor(logLevel);

            textWriter.Write(logLevelColor);
            textWriter.Write(logLevelString);
            textWriter.Write(": ");
            textWriter.Write("\u001b[0m"); // Reset color

            var scopeWritten = false;

            scopeProvider?.ForEachScope((scope, state) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> dictionary)
                    {
                        if (!scopeWritten)
                        {
                            foreach (var item in dictionary)
                            {
                                if (item.Key == "TraceIdentifier" || item.Key == "RequestPath")
                                {
                                    textWriter.Write($"{item.Key}: {item.Value} ");
                                }
                            }
                            scopeWritten = true; // Ensure scope is written only once
                        }
                    }
                }, logEntry);

            textWriter.WriteLine(message);
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }

        private static string GetLogLevelColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "\u001b[37m",    // White
                LogLevel.Debug => "\u001b[34m",    // Blue
                LogLevel.Information => "\u001b[32m", // Green
                LogLevel.Warning => "\u001b[33m",  // Yellow
                LogLevel.Error => "\u001b[31m",    // Red
                LogLevel.Critical => "\u001b[35m", // Magenta
                _ => "\u001b[0m"                   // Reset
            };
        }
    }
}
