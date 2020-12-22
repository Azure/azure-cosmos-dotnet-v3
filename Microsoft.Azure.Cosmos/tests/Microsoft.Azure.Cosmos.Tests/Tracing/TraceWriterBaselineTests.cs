//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests.Query.Metrics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class TraceWriterBaselineTests : BaselineTests<TraceWriterBaselineTests.Input, TraceWriterBaselineTests.Output>
    {
        private static readonly QueryMetrics MockQueryMetrics = new QueryMetrics(
            BackendMetricsTests.MockBackendMetrics,
            IndexUtilizationInfoTests.MockIndexUtilizationInfo,
            ClientSideMetricsTests.MockClientSideMetrics);

        [TestMethod]
        public void Serialization()
        {
            List<Input> inputs = new List<Input>();

            int startLineNumber;
            int endLineNumber;

            //----------------------------------------------------------------
            //  Root Trace
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Datum
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTraceWithDatum;
                using (rootTraceWithDatum = TraceForBaselineTesting.GetRootTrace())
                {
                    rootTraceWithDatum.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Datum", rootTraceWithDatum, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With One Child
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                Trace rootTrace;
                using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With One Child", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With One Child With Datum
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                Trace rootTrace;
                using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                        childTrace1.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With One Child With Datum", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Two Children
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                Trace rootTrace;
                using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                    {
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Two Children", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Root Trace With Two Children With Info
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                Trace rootTrace;
                using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
                {
                    using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                    {
                        childTrace1.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                    {
                        childTrace2.AddDatum("QueryMetrics", new QueryMetricsTraceDatum(MockQueryMetrics));
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Root Trace With Two Children With Info", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Trace With Grandchidren
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    using (ITrace childTrace1 = rootTrace.StartChild(
                        name: "Child1",
                        component: TraceComponent.Unknown,
                        level: TraceLevel.Info))
                    {
                        using (ITrace child1Child1 = childTrace1.StartChild(
                            name: "Child1Child1",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child1Child2 = childTrace1.StartChild(
                            name: "Child1Child2",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }
                    }

                    using (ITrace childTrace2 = rootTrace.StartChild(
                        name: "Child2",
                        component: TraceComponent.Unknown,
                        level: TraceLevel.Info))
                    {
                        using (ITrace child2Child1 = childTrace2.StartChild(
                            name: "Child2Child1",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child2Child2 = childTrace2.StartChild(
                            name: "Child2Child2",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }

                        using (ITrace child2Child3 = childTrace2.StartChild(
                            name: "Child2Child3",
                            component: TraceComponent.Unknown,
                            level: TraceLevel.Info))
                        {
                        }
                    }
                }

                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Trace With Grandchildren", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------
        }

        public override Output ExecuteTest(Input input)
        {
            string text = TraceWriter.TraceToText(input.Trace);
            string json = TraceWriter.TraceToJson(input.Trace);

            return new Output(text, json);
        }

        private static int GetLineNumber([CallerLineNumber] int lineNumber = 0)
        {
            return lineNumber;
        }

        public sealed class Input : BaselineTestInput
        {
            private static readonly string[] sourceCode = File.ReadAllLines(nameof(TraceWriterBaselineTests) + ".cs");

            internal Input(string description, ITrace trace, int startLineNumber, int endLineNumber)
                : base(description)
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
                this.StartLineNumber = startLineNumber;
                this.EndLineNumber = endLineNumber;
            }

            internal ITrace Trace { get; }

            public int StartLineNumber { get; }

            public int EndLineNumber { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteStartElement("Source Code");
                ArraySegment<string> codeSnippet = new ArraySegment<string>(
                    sourceCode,
                    this.StartLineNumber,
                    this.EndLineNumber - this.StartLineNumber - 1);
                string setup =
                    Environment.NewLine
                    + string
                        .Join(
                            Environment.NewLine,
                            codeSnippet
                                .Select(x => x.Substring("            ".Length)))
                    + Environment.NewLine;
                xmlWriter.WriteCData(setup);
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class Output : BaselineTestOutput
        {
            public Output(string text, string json)
            {
                this.Text = text ?? throw new ArgumentNullException(nameof(text));
                this.Json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public string Text { get; }

            public string Json { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(this.Text));
                xmlWriter.WriteCData(this.Text);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement(nameof(this.Json));
                xmlWriter.WriteCData(this.Json);
                xmlWriter.WriteEndElement();
            }
        }

        private sealed class TraceForBaselineTesting : ITrace
        {
            private readonly Dictionary<string, object> data;
            private readonly List<TraceForBaselineTesting> children;

            public TraceForBaselineTesting(
                string name,
                TraceLevel level,
                TraceComponent component,
                TraceForBaselineTesting parent)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.Level = level;
                this.Component = component;
                this.Parent = parent;
                this.children = new List<TraceForBaselineTesting>();
                this.data = new Dictionary<string, object>();
            }

            public string Name { get; }

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => new CallerInfo("MemberName", "FilePath", 42);

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level { get; }

            public TraceComponent Component { get; }

            public ITrace Parent { get; }

            public IReadOnlyList<ITrace> Children => null;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
            }

            public void AddDatum(string key, object value)
            {
                this.data[key] = value;
            }

            public void Dispose()
            {
            }

            public ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info, memberName, sourceFilePath, sourceLineNumber);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                TraceForBaselineTesting child = new TraceForBaselineTesting(name, level, component, parent: this);
                this.children.Add(child);
                return child;
            }

            public static TraceForBaselineTesting GetRootTrace()
            {
                return new TraceForBaselineTesting("Trace For Baseline Testing", TraceLevel.Info, TraceComponent.Unknown, parent: null);
            }
        }
    }
}
