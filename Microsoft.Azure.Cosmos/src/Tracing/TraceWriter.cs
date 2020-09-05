// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    internal static class TraceWriter
    {
        private const string space = "  ";

        private static readonly AsciiTreeCharacters DefaultAsciiTreeCharacters = new AsciiTreeCharacters(
            blank: ' ',
            child: '├',
            dash: '─',
            last: '└',
            parent: '│',
            root: '.');
        private static readonly AsciiTreeIndents DefaultAsciiTreeIndents = AsciiTreeIndents.Create(DefaultAsciiTreeCharacters);
        private static readonly string[] newLines = new string[] { Environment.NewLine };
        private static readonly char[] newLineCharacters = Environment.NewLine.ToCharArray();

        public static void WriteTrace(TextWriter writer, ITrace trace, TraceLevel level = TraceLevel.Verbose)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if ((int)trace.Level < (int)level)
            {
                return;
            }

            writer.WriteLine(DefaultAsciiTreeCharacters.Root);
            WriteTraceRecursive(writer, trace, level, isLastChild: true);
        }

        private static void WriteTraceRecursive(TextWriter writer, ITrace trace, TraceLevel level, bool isLastChild)
        {
            ITrace parent = trace.Parent;
            Stack<string> indentStack = new Stack<string>();
            while (parent != null)
            {
                bool parentIsLastChild = (parent.Parent == null) || parent.Equals(parent.Parent.Children.Last());
                if (parentIsLastChild)
                {
                    indentStack.Push(DefaultAsciiTreeIndents.Blank);
                }
                else
                {
                    indentStack.Push(DefaultAsciiTreeIndents.Parent);
                }

                parent = parent.Parent;
            }

            foreach (string indent in indentStack)
            {
                writer.Write(indent);
            }

            if (isLastChild)
            {
                writer.Write(DefaultAsciiTreeIndents.Last);
            }
            else
            {
                writer.Write(DefaultAsciiTreeIndents.Child);
            }

            writer.Write(trace.Name);
            writer.Write('(');
            writer.Write(trace.Id);
            writer.Write(')');
            writer.Write(space);

            writer.Write(trace.Component);
            writer.Write('-');
            writer.Write("Component");
            writer.Write(space);

            writer.Write(trace.StackFrame.GetFileName().Split('\\').Last());
            writer.Write(':');
            writer.Write(trace.StackFrame.GetFileLineNumber());
            writer.Write(space);

            writer.Write(trace.StartTime.ToString("hh:mm:ss:fff"));
            writer.Write(space);

            writer.Write(trace.Duration.TotalMilliseconds.ToString("0.00"));
            writer.Write(" milliseconds");
            writer.Write(space);

            writer.WriteLine();

            if (trace.Info != null)
            {
                {
                    foreach (string indent in indentStack)
                    {
                        writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    writer.WriteLine('(');
                }

                string[] infoLines = trace.Info
                    .Serialize()
                    .TrimEnd(newLineCharacters)
                    .Split(newLines, StringSplitOptions.None);
                foreach (string infoLine in infoLines)
                {
                    foreach (string indent in indentStack)
                    {
                        writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    writer.Write(DefaultAsciiTreeIndents.Blank);

                    writer.WriteLine(infoLine);
                }

                {
                    foreach (string indent in indentStack)
                    {
                        writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    writer.WriteLine(')');
                }
            }

            for (int i = 0; i < trace.Children.Count - 1; i++)
            {
                ITrace child = trace.Children[i];
                WriteTraceRecursive(writer, child, level, isLastChild: false);
            }

            if (trace.Children.Count != 0)
            {
                ITrace child = trace.Children[trace.Children.Count - 1];
                WriteTraceRecursive(writer, child, level, isLastChild: true);
            }
        }

        /// <summary>
        /// Character set to generate an Ascii Tree (https://marketplace.visualstudio.com/items?itemName=aprilandjan.ascii-tree-generator)
        /// </summary>
        private readonly struct AsciiTreeCharacters
        {
            public AsciiTreeCharacters(char blank, char child, char dash, char last, char parent, char root)
            {
                this.Blank = blank;
                this.Child = child;
                this.Dash = dash;
                this.Last = last;
                this.Parent = parent;
                this.Root = root;
            }

            /// <summary>
            /// For blanks / spaces
            /// </summary>
            public char Blank { get; }

            /// <summary>
            /// For intermediate child elements
            /// </summary>
            public char Child { get; }

            /// <summary>
            /// For horizontal dashes
            /// </summary>
            public char Dash { get; }

            /// <summary>
            /// For the last element of a path
            /// </summary>
            public char Last { get; }

            /// <summary>
            /// For vertical parent elements
            /// </summary>
            public char Parent { get; }

            /// <summary>
            /// For the root element (on top)
            /// </summary>
            public char Root { get; }
        }

        private readonly struct AsciiTreeIndents
        {
            private AsciiTreeIndents(string child, string parent, string last, string blank)
            {
                this.Child = child;
                this.Parent = parent;
                this.Last = last;
                this.Blank = blank;
            }

            public string Child { get; }

            public string Parent { get; }

            public string Last { get; }

            public string Blank { get; }

            public static AsciiTreeIndents Create(AsciiTreeCharacters asciiTreeCharacters) => new AsciiTreeIndents(
                child: new string(
                    new char[]
                    {
                        asciiTreeCharacters.Child,
                        asciiTreeCharacters.Dash,
                        asciiTreeCharacters.Dash,
                        asciiTreeCharacters.Blank
                    }),
                parent: new string(
                    new char[]
                    {
                        asciiTreeCharacters.Parent,
                        asciiTreeCharacters.Blank,
                        asciiTreeCharacters.Blank,
                        asciiTreeCharacters.Blank
                    }),
                last: new string(
                    new char[]
                    {
                        asciiTreeCharacters.Last,
                        asciiTreeCharacters.Dash,
                        asciiTreeCharacters.Dash,
                        asciiTreeCharacters.Blank
                    }),
                blank: new string(
                    new char[]
                    {
                        asciiTreeCharacters.Blank,
                        asciiTreeCharacters.Blank,
                        asciiTreeCharacters.Blank,
                        asciiTreeCharacters.Blank
                    }));
        }
    }
}
