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

    internal sealed class TraceWriter
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

        private readonly TextWriter writer;
        private int indentLevel;

        public TraceWriter(TextWriter writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void WriteTrace(ITrace trace, TraceLevel level = TraceLevel.Verbose)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if ((int)trace.Level < (int)level)
            {
                return;
            }

            this.writer.WriteLine(DefaultAsciiTreeCharacters.Root);
            this.WriteTraceRecursive(trace, level, isLastChild: true);
        }

        private void WriteTraceRecursive(ITrace trace, TraceLevel level, bool isLastChild)
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
                this.writer.Write(indent);
            }

            if (isLastChild)
            {
                this.writer.Write(DefaultAsciiTreeIndents.Last);
            }
            else
            {
                this.writer.Write(DefaultAsciiTreeIndents.Child);
            }

            this.writer.Write(trace.Name);
            this.writer.Write('(');
            this.writer.Write(trace.Id);
            this.writer.Write(')');
            this.writer.Write(space);

            this.writer.Write(trace.Component);
            this.writer.Write('-');
            this.writer.Write("Component");
            this.writer.Write(space);

            this.writer.Write(trace.StackFrame.GetFileName().Split('\\').Last());
            this.writer.Write(':');
            this.writer.Write(trace.StackFrame.GetFileLineNumber());
            this.writer.Write(space);

            this.writer.Write(trace.StartTime.ToString("hh:mm:ss:fff"));
            this.writer.Write(space);

            this.writer.Write(trace.Duration.TotalMilliseconds.ToString("0.00"));
            this.writer.Write(" milliseconds");
            this.writer.Write(space);

            this.writer.WriteLine();

            if (trace.Info != null)
            {
                {
                    foreach (string indent in indentStack)
                    {
                        this.writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    this.writer.WriteLine('(');
                }

                string[] infoLines = trace.Info
                    .Serialize()
                    .TrimEnd(newLineCharacters)
                    .Split(newLines, StringSplitOptions.None);
                foreach (string infoLine in infoLines)
                {
                    foreach (string indent in indentStack)
                    {
                        this.writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    this.writer.Write(DefaultAsciiTreeIndents.Blank);

                    this.writer.WriteLine(infoLine);
                }

                {
                    foreach (string indent in indentStack)
                    {
                        this.writer.Write(indent);
                    }

                    if (isLastChild)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    if (trace.Children.Count == 0)
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Blank);
                    }
                    else
                    {
                        this.writer.Write(DefaultAsciiTreeIndents.Parent);
                    }

                    this.writer.WriteLine(')');
                }
            }

            this.indentLevel++;

            for (int i = 0; i < trace.Children.Count - 1; i++)
            {
                ITrace child = trace.Children[i];
                this.WriteTraceRecursive(child, level, isLastChild: false);
            }

            if (trace.Children.Count != 0)
            {
                ITrace child = trace.Children[trace.Children.Count - 1];
                this.WriteTraceRecursive(child, level, isLastChild: true);
            }

            this.indentLevel--;
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
