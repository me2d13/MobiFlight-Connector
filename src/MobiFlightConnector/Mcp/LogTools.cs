using ModelContextProtocol.Server;
using System;
using System.ComponentModel;

namespace MobiFlight.Mcp
{
    /// <summary>
    /// Read-only access to the MobiFlight log file; the delegate is
    /// injected at startup so the tool stays unit-testable.
    /// </summary>
    [McpServerToolType]
    public static class LogTools
    {
        private static Func<int, string> _getLogTail;

        public static void Initialize(Func<int, string> getLogTail)
        {
            _getLogTail = getLogTail;
        }

        internal static void Reset()
        {
            _getLogTail = null;
        }

        [McpServerTool, Description("Returns the last N lines of the MobiFlight log file. Use this to analyze what happened - e.g. after triggering actions, connecting devices, or loading a project. Timestamps and severity levels are included in each line.")]
        public static string GetLog([Description("Number of log lines to return, counting from the end of the file.")] int lineCount = 200)
        {
            if (_getLogTail is null)
                throw new InvalidOperationException("LogTools not initialized.");

            if (lineCount < 1) lineCount = 1;
            if (lineCount > 2000) lineCount = 2000;

            return _getLogTail(lineCount);
        }
    }
}
