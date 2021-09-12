// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal static partial class TraceWriter
    {
        private static readonly ConcurrentDictionary<string, string> FilePathToName = new ConcurrentDictionary<string, string>();

        public static void WriteTrace(
            TextWriter writer,
            ITrace trace,
            TraceLevel level = TraceLevel.Verbose,
            AsciiType asciiType = AsciiType.Default)
        {
            TraceTextWriter.WriteTrace(
                writer,
                trace,
                level,
                asciiType);
        }

        public static void WriteTrace(
            IJsonWriter writer,
            ITrace trace)
        {
            TraceJsonWriter.WriteTrace(writer, trace);
        }

        public static string TraceToText(
            ITrace trace,
            TraceLevel level = TraceLevel.Verbose,
            AsciiType asciiType = AsciiType.Default)
        {
            StringWriter writer = new StringWriter();
            WriteTrace(writer, trace, level, asciiType);
            return writer.ToString();
        }

        public static string TraceToJson(
            ITrace trace)
        {
            IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
            WriteTrace(writer, trace);
            return Encoding.UTF8.GetString(writer.GetResult().ToArray());
        }

        public enum AsciiType
        {
            Default,
            DoubleLine,
            Classic,
            ClassicRounded,
            ExclamationMarks,
        }

        public static string GetFileNameFromPath(string filePath)
        {
            if (!FilePathToName.TryGetValue(filePath, out string fileName))
            {
                fileName = filePath.Split('\\').Last();
                FilePathToName[filePath] = fileName;
            }

            return fileName;
        }
    }
}
