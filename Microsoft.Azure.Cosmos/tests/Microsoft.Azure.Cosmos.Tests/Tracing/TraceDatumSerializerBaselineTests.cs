namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Test.SqlObjects;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class TraceDatumSerializerBaselineTests : BaselineTests<TraceDatumSerializerBaselineTests.Input, TraceDatumSerializerBaselineTests.Output>
    {
        [TestMethod]
        public void Tests()
        {
            List<Input> inputs = new List<Input>
            {
                new Input(
                    "Point Operation Statistics", 
                    new PointOperationStatisticsTraceDatum(
                        activityId: Guid.Empty.ToString(),
                        responseTimeUtc: new DateTime(2020, 1, 2, 3, 4, 5, 6),
                        statusCode: System.Net.HttpStatusCode.OK,
                        subStatusCode: Documents.SubStatusCodes.WriteForbidden,
                        requestCharge: 4,
                        errorMessage: null,
                        method: HttpMethod.Post,
                        requestUri: "http://localhost.com",
                        requestSessionToken: nameof(PointOperationStatisticsTraceDatum.RequestSessionToken),
                        responseSessionToken: nameof(PointOperationStatisticsTraceDatum.ResponseSessionToken))),
            };

            this.ExecuteTestSuite(inputs);
        }

        public override Output ExecuteTest(Input input)
        {
            BaselineTrace baselineTrace = new BaselineTrace();
            baselineTrace.AddDatum("Datum", input.Datum);

            string text = TraceWriter.TraceToText(baselineTrace);
            string json = TraceWriter.TraceToJson(baselineTrace);

            return new Output(text, json);
        }

        private sealed class BaselineTrace : ITrace
        {
            private readonly Dictionary<string, object> data;

            public BaselineTrace()
            {
                this.data = new Dictionary<string, object>();
            }

            public string Name => "Trace for baseline testing";

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => new CallerInfo("MemberName", "FilePath", 42);

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level => TraceLevel.Info;

            public TraceComponent Component => TraceComponent.Unknown;

            public ITrace Parent => null;

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
                throw new NotImplementedException();
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                throw new NotImplementedException();
            }
        }

        public sealed class Input : BaselineTestInput
        {
            internal Input(string description, TraceDatum datum)
                : base(description)
            {
                this.Datum = datum ?? throw new ArgumentNullException(nameof(datum));
            }

            internal TraceDatum Datum { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
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
    }
}