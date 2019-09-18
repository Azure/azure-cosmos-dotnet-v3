namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Placeholder for VST Logger.
    /// </summary>
    internal static class Logger
    {
        public static void LogLine(string message)
        {
            Debug.WriteLine(message);
            Trace.WriteLine(message);
        }

        public static void LogLine(string format, params object[] parameters)
        {
            Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, format, parameters));
            Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, format, parameters));
        }
    }
}
