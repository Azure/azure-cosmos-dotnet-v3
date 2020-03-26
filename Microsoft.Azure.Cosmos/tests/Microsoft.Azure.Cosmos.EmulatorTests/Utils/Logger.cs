namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;

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

        public static string DumpFullExceptionMessage(Exception e)
        {
            StringBuilder exceptionMessage = new StringBuilder();
            while (e != null)
            {
                DocumentClientException docException = e as DocumentClientException;
                if (docException != null && docException.Error != null)
                {
                    exceptionMessage.Append("Code : " + docException.Error.Code);
                    if (docException.Error.ErrorDetails != null)
                    {
                        exceptionMessage.AppendLine(" ; Details : " + docException.Error.ErrorDetails);
                    }
                    if (docException.Error.Message != null)
                    {
                        exceptionMessage.AppendLine(" ; Message : " + docException.Error.Message);
                    }
                    exceptionMessage.Append(" --> ");
                }

                e = e.InnerException;
            }

            return exceptionMessage.ToString();
        }
    }
}
