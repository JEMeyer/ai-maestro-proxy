using Microsoft.Extensions.Logging.Abstractions;

namespace AIMaestroProxy.Logging
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Console;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class CustomConsoleFormatter : ConsoleFormatter
    {
        public CustomConsoleFormatter() : base("custom") { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var logLevel = logEntry.LogLevel;
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            var logLevelString = GetLogLevelString(logLevel);
            var logLevelColor = GetLogLevelColor(logLevel);
            var traceIdentifierColor = "\u001b[36m"; // Cyan

            textWriter.Write(logLevelColor);
            textWriter.Write(logLevelString);
            textWriter.Write(": ");
            textWriter.Write("\u001b[0m"); // Reset color

            var traceIdentifier = "N/A";

            // Modified scope handling
            var scopeValues = new Dictionary<string, string>();
            scopeProvider?.ForEachScope((scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> dictionary)
                {
                    foreach (var item in dictionary)
                    {
                        // Always update with the latest value
                        scopeValues[item.Key] = item.Value?.ToString() ?? "N/A";
                    }
                }
            }, logEntry);

            // Get the final values after processing all scopes
            if (scopeValues.TryGetValue("TraceIdentifier", out var trace))
            {
                traceIdentifier = trace;
            }

            if (traceIdentifier == "N/A")
            {
                textWriter.Write(traceIdentifierColor);
                textWriter.Write("[GLOBAL] ");
                textWriter.Write("\u001b[0m"); // Reset color
            }
            else
            {
                textWriter.Write(traceIdentifierColor);
                textWriter.Write($"TraceIdentifier: {traceIdentifier} ");
                textWriter.Write("\u001b[0m"); // Reset color
            }

            // Check for the color marker in the message and apply color if found
            if (message.Contains("##COLOR##"))
            {
                message = message.Replace("##COLOR##", "\u001b[93m"); // Apply yellow color to the message
                textWriter.WriteLine(message + "\u001b[0m"); // Reset color
            }
            else
            {
                textWriter.WriteLine(message);
            }
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
