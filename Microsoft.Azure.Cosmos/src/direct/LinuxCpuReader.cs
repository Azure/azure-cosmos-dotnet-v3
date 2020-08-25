//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using Microsoft.Azure.Cosmos.Core.Trace;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    internal sealed class LinuxCpuReader : CpuReaderBase
    {
        private readonly ProcStatFileParser procStatFileParser;

        private ulong lastIdleJiffies;
        private ulong lastKernelJiffies;
        private ulong lastOtherJiffies;
        private ulong lastUserJiffies;

        public LinuxCpuReader() : this(procStatFilePath: null)
        {
        }

        internal LinuxCpuReader(string procStatFilePath)
        {
            if (String.IsNullOrWhiteSpace(procStatFilePath))
            {
                this.procStatFileParser = new ProcStatFileParser();
            }
            else
            {
                this.procStatFileParser = new ProcStatFileParser(procStatFilePath);
            }

            this.lastIdleJiffies = 0;
            this.lastKernelJiffies = 0;
            this.lastOtherJiffies = 0;
            this.lastUserJiffies = 0;
        }

        protected override float GetSystemWideCpuUsageCore()
        {
            ulong currentUserJiffies;
            ulong currentKernelJiffies;
            ulong currentIdleJiffies;
            ulong currentOtherJiffies;
            if (!this.procStatFileParser.TryParseStatFile(
                    out currentUserJiffies,
                    out currentKernelJiffies,
                    out currentIdleJiffies,
                    out currentOtherJiffies))
            {
                return Single.NaN;
            }

            float totalCpuUsage = 0;

            if (this.lastIdleJiffies != 0)
            {
                ulong kernelJiffiesElapsed = currentKernelJiffies - this.lastKernelJiffies;
                ulong userJiffiesElapsed = currentUserJiffies - this.lastUserJiffies;
                ulong busyJiffiesElapsed = kernelJiffiesElapsed +
                    userJiffiesElapsed +
                    currentOtherJiffies - this.lastOtherJiffies;

                ulong jiffiesElapsed = busyJiffiesElapsed +
                    currentIdleJiffies -
                    this.lastIdleJiffies;

                if (jiffiesElapsed == 0)
                {
                    // Can only ever happen when procfs content is identical between two calls
                    // and not even the idle jiffies have increased. In this case CPU utilization
                    // cannot be determined and the API isn't meant to be called in a tight loop anyway
                    return Single.NaN;
                }

                totalCpuUsage = 100 * (busyJiffiesElapsed / (float)jiffiesElapsed);
            }

            this.lastUserJiffies = currentUserJiffies;
            this.lastKernelJiffies = currentKernelJiffies;
            this.lastIdleJiffies = currentIdleJiffies;
            this.lastOtherJiffies = currentOtherJiffies;

            return totalCpuUsage;
        }

        private class ProcStatFileParser
        {
            const string cpuPrefixFirstLine = "cpu";
            private const string DefaultProcStatFilePath = "/proc/stat";

            private readonly string procStatFilePath;
            private readonly ReusableTextReader reusableReader;

            public ProcStatFileParser() : this(DefaultProcStatFilePath)
            {
            }

            /// <summary>
            /// Allows customization of the proc stat file path to allow testing this on Non-Linux machines
            /// </summary>
            /// <param name="procStatFilePath">
            /// </param>
            internal ProcStatFileParser(string procStatFilePath)
            {
                if (String.IsNullOrWhiteSpace(procStatFilePath))
                {
                    throw new ArgumentNullException(nameof(procStatFilePath));
                }

                this.reusableReader = new ReusableTextReader(Encoding.UTF8, bufferSize: 256);
                this.procStatFilePath = procStatFilePath;
            }

            public bool TryParseStatFile(
                out ulong userJiffiesElaped,
                out ulong kernelJiffiesElapsed,
                out ulong idleJiffiesElapsed,
                out ulong otherJiffiesElapsed)
            {
                userJiffiesElaped = 0;
                kernelJiffiesElapsed = 0;
                idleJiffiesElapsed = 0;
                otherJiffiesElapsed = 0;

                string statFileFirstLine;
                if (!this.TryReadProcStatFirstLine(this.reusableReader, out statFileFirstLine))
                {
                    return false;
                }

                try
                {
                    StringParser parser = new StringParser(statFileFirstLine, ' ', skipEmpty: true);

                    string prefix = parser.MoveAndExtractNext();
                    if (!cpuPrefixFirstLine.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        DefaultTrace.TraceCritical($"Unexpected procfs/cpu-file format. '${statFileFirstLine}'");
                        return false;
                    }

                    ulong user = parser.ParseNextUInt64();
                    ulong nice = parser.ParseNextUInt64();
                    ulong kernel = parser.ParseNextUInt64();
                    ulong idle = parser.ParseNextUInt64();

                    // according to http://www.linuxhowtos.org/manpages/5/proc.htm currently the cpu-line
                    // can have up to 9 columns with different processore state aggregates. It is posisble
                    // that this will change in later Linux versions and new columns are added
                    ulong others = 0;
                    while (parser.HasNext)
                    {
                        others += parser.ParseNextUInt64();
                    }

                    userJiffiesElaped = user + nice;
                    kernelJiffiesElapsed = kernel;
                    idleJiffiesElapsed = idle;
                    otherJiffiesElapsed = others;

                    return true;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
            }

            private bool TryReadProcStatFirstLine(ReusableTextReader reusableReader, out string firstLine)
            {
                try
                {
                    // ProcFS is a virtual file system. Content is kept in memory
                    // so no I/O when reading this FileStream --> not using async
                    using (FileStream fileStream = new FileStream(
                        this.procStatFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 1,
                        useAsync: false))
                    {
                        firstLine = reusableReader.ReadJustFirstLine(fileStream);
                        return true;
                    }
                }
                catch (IOException)
                {
                    firstLine = null;
                    return false;
                }
            }

            /// <summary>
            /// Provides a string parser that may be used instead of String.Split to avoid unnecessary
            /// string and array allocations.
            /// </summary>
            /// <remarks>
            /// This class has been ported from the dotnet/runtime repository here
            /// https://github.com/dotnet/corefx/blob/master/src/Common/src/System/IO/StringParser.cs
            /// </remarks>
            private struct StringParser
            {
                /// <summary>
                /// The string being parsed.
                /// </summary>
                private readonly string buffer;

                /// <summary>
                /// The separator character used to separate subcomponents of the larger string.
                /// </summary>
                private readonly char separator;

                /// <summary>
                /// true if empty subcomponents should be skipped; false to treat them as valid entries.
                /// </summary>
                private readonly bool skipEmpty;

                /// <summary>
                /// The ending index that represents the next index after the last character that's part
                /// of the current entry.
                /// </summary>
                private int endIndex;

                /// <summary>
                /// The starting index from which to parse the current entry.
                /// </summary>
                private int startIndex;

                /// <summary>
                /// Initialize the StringParser.
                /// </summary>
                /// <param name="buffer">
                /// The string to parse.
                /// </param>
                /// <param name="separator">
                /// The separator character used to separate subcomponents of <paramref name="buffer" />.
                /// </param>
                /// <param name="skipEmpty">
                /// true if empty subcomponents should be skipped; false to treat them as valid entries.
                /// Defaults to false.
                /// </param>
                public StringParser(string buffer, char separator, bool skipEmpty = false)
                {
                    if (buffer == null)
                    {
                        throw new ArgumentNullException(nameof(buffer));
                    }

                    this.buffer = buffer;
                    this.separator = separator;
                    this.skipEmpty = skipEmpty;
                    this.startIndex = -1;
                    this.endIndex = -1;
                }

                public bool HasNext
                {
                    get
                    {
                        return this.endIndex < this.buffer.Length;
                    }
                }

                /// <summary>
                /// Gets the current subcomponent of the string as a string.
                /// </summary>
                public string ExtractCurrent()
                {
                    if (this.buffer == null || this.startIndex == -1)
                    {
                        throw new InvalidOperationException();
                    }

                    return this.buffer.Substring(this.startIndex, this.endIndex - this.startIndex);
                }

                /// <summary>
                /// Moves to the next component of the string and returns it as a string.
                /// </summary>
                /// <returns>
                /// </returns>
                public string MoveAndExtractNext()
                {
                    this.MoveNextOrFail();
                    return this.buffer.Substring(this.startIndex, this.endIndex - this.startIndex);
                }

                /// <summary>
                /// Moves to the next component of the string.
                /// </summary>
                /// <returns>
                /// true if there is a next component to be parsed; otherwise, false.
                /// </returns>
                public bool MoveNext()
                {
                    if (this.buffer == null)
                    {
                        throw new InvalidOperationException();
                    }

                    while (true)
                    {
                        if (this.endIndex >= this.buffer.Length)
                        {
                            this.startIndex = this.endIndex;
                            return false;
                        }

                        int nextSeparator = this.buffer.IndexOf(this.separator, this.endIndex + 1);
                        this.startIndex = this.endIndex + 1;
                        this.endIndex = nextSeparator >= 0 ? nextSeparator : this.buffer.Length;

                        if (!this.skipEmpty || this.endIndex >= this.startIndex + 1)
                        {
                            return true;
                        }
                    }
                }

                /// <summary>
                /// Moves to the next component of the string. If there isn't one, it throws an exception.
                /// </summary>
                public void MoveNextOrFail()
                {
                    if (!this.MoveNext())
                    {
                        ThrowForInvalidData();
                    }
                }

                /// <summary>
                /// Moves to the next component and parses it as a UInt64.
                /// </summary>
                public unsafe ulong ParseNextUInt64()
                {
                    this.MoveNextOrFail();

                    ulong result = 0;
                    fixed (char* bufferPtr = this.buffer)
                    {
                        char* p = bufferPtr + this.startIndex;
                        char* end = bufferPtr + this.endIndex;
                        while (p != end)
                        {
                            int d = *p - '0';
                            if (d < 0 || d > 9)
                            {
                                ThrowForInvalidData();
                            }
                            result = checked((result * 10ul) + (ulong)d);

                            p++;
                        }
                    }

                    Debug.Assert(result == UInt64.Parse(this.ExtractCurrent(), CultureInfo.InvariantCulture),
                        "Expected manually parsed result to match Parse result");

                    return result;
                }

                /// <summary>
                /// Throws unconditionally for invalid data.
                /// </summary>
                private static void ThrowForInvalidData()
                {
                    throw new InvalidDataException();
                }
            }

            /// <summary>
            /// Provides a reusable reader for reading all of the text from streams.
            /// </summary>
            private sealed class ReusableTextReader
            {
                private static readonly char[] lineBreakChars = Environment.NewLine.ToCharArray();

                /// <summary>
                /// StringBuilder used to store intermediate text results.
                /// </summary>
                private readonly StringBuilder builder;

                /// <summary>
                /// Bytes read from the stream.
                /// </summary>
                private readonly byte[] bytes;

                /// <summary>
                /// Temporary storage from characters converted from the bytes then written to the builder.
                /// </summary>
                private readonly char[] chars;

                /// <summary>
                /// Decoder used to decode data read from the stream.
                /// </summary>
                private readonly Decoder decoder;

                /// <summary>
                /// Initializes a new reusable reader.
                /// </summary>
                /// <param name="encoding">
                /// The Encoding to use. Defaults to UTF8.
                /// </param>
                /// <param name="bufferSize">
                /// The size of the buffer to use when reading from the stream.
                /// </param>
                public ReusableTextReader(Encoding encoding = null, int bufferSize = 1024)
                {
                    if (encoding == null)
                    {
                        encoding = Encoding.UTF8;
                    }

                    this.builder = new StringBuilder();
                    this.decoder = encoding.GetDecoder();
                    this.bytes = new byte[bufferSize];
                    this.chars = new char[encoding.GetMaxCharCount(this.bytes.Length)];
                }

                public string ReadJustFirstLine(Stream source)
                {
                    int bytesRead;
                    while ((bytesRead = source.Read(this.bytes, 0, this.bytes.Length)) != 0)
                    {
                        int charCount = this.decoder.GetChars(this.bytes, 0, bytesRead, this.chars, 0);
                        int lineFeedIndex = -1;

                        for (int i = 0; i < charCount; i++)
                        {
                            // Except in test scenarios lineBreakChars will only contain one character '\n' (Linux)
                            // when running tests on Windows recognize any of the chars indicating new line '\r\n'
                            if (lineBreakChars.Contains(this.chars[i]))
                            {
                                lineFeedIndex = i;
                                break;
                            }
                        }

                        if (lineFeedIndex < 0)
                        {
                            this.builder.Append(this.chars, 0, charCount);
                        }
                        else
                        {
                            this.builder.Append(this.chars, 0, lineFeedIndex);
                            break;
                        }
                    }

                    string s = this.builder.ToString();

                    this.builder.Clear();
                    this.decoder.Reset();

                    return s;
                }
            }
        }
    }
}