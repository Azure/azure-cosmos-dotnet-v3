//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Tokens;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PipelineContinuationTokenTests : BaselineTests<PipelineContinuationTokenTests.PipelineContinuationTokenTestsInput, PipelineContinuationTokenTests.PipelineContinuationTokenTestsOutput>
    {
        [TestMethod]
        [Owner("brchon")]
        public void Tests()
        {
            List<PipelineContinuationTokenTests.PipelineContinuationTokenTestsInput> pipelineContinuationTokenTestsInputs = new List<PipelineContinuationTokenTestsInput>
            {
                // Positive Tests
                new PipelineContinuationTokenTestsInput(
                    description: "V0 Continuation Token",
                    continuationToken: @"[{""asdf"": ""asdf""}]"),
                new PipelineContinuationTokenTestsInput(
                    description: "V1 Continuation Token",
                    continuationToken: "{\"Version\":\"1.0\",\"SourceContinuationToken\":\"{\\\"asdf\\\": \\\"asdf\\\"}\"}"),
                new PipelineContinuationTokenTestsInput(
                    description: "V1.1 Continuation Token",
                    continuationToken: "{\"Version\":\"1.1\",\"QueryPlan\":null,\"SourceContinuationToken\":\"{\\\"asdf\\\": \\\"asdf\\\"}\"}"),

                // V0 by default
                new PipelineContinuationTokenTestsInput(
                    description: "V0 Invalid JSON",
                    continuationToken: @"{""asdf"": ..."),
                new PipelineContinuationTokenTestsInput(
                    description: "V0 Valid JSON but not an object",
                    continuationToken: @"42"),

                // Negative Tests
                new PipelineContinuationTokenTestsInput(
                    description: "Invalid Version Number.",
                    continuationToken: @"{""Version"": ""42.1337""}"),

                // Version 1 Negative Tests
                new PipelineContinuationTokenTestsInput(
                    description: "V1 No Source Continuation Token",
                    continuationToken: "{\"Version\":\"1.0\"}"),

                // Version 1.1 Negative Tests
                new PipelineContinuationTokenTestsInput(
                    description: "V1.1 No Query Plan",
                    continuationToken: "{\"Version\":\"1.1\",\"SourceContinuationToken\":\"{\\\"asdf\\\": \\\"asdf\\\"}\"}"),
                new PipelineContinuationTokenTestsInput(
                    description: "V1.1 Query Plan Malformed",
                    continuationToken: "{\"Version\":\"1.1\",\"QueryPlan\": 42,\"SourceContinuationToken\":\"{\\\"asdf\\\": \\\"asdf\\\"}\"}"),
                new PipelineContinuationTokenTestsInput(
                    description: "V1.1 No Source Continuation",
                    continuationToken: "{\"Version\":\"1.1\",\"QueryPlan\": null}"),
            };

            this.ExecuteTestSuite(pipelineContinuationTokenTestsInputs);
        }

        public override PipelineContinuationTokenTestsOutput ExecuteTest(
            PipelineContinuationTokenTestsInput input)
        {
            TryCatch<CosmosElement> tryParse = CosmosElement.Monadic.Parse(input.ContinuationToken);
            if (tryParse.Failed)
            {
                return new PipelineContinuationTokenTestsOutputNegative("Failed to parse token.");
            }

            if (!PipelineContinuationToken.TryCreateFromCosmosElement(
                tryParse.Result,
                out PipelineContinuationToken pipelineContinuationToken))
            {
                return new PipelineContinuationTokenTestsOutputNegative("Failed to parse token.");
            }

            if (!PipelineContinuationToken.TryConvertToLatest(
                pipelineContinuationToken,
                out PipelineContinuationTokenV1_1 latestPipelineContinuationToken))
            {
                return new PipelineContinuationTokenTestsOutputNegative("Failed to convert to latest");
            }

            return new PipelineContinuationTokenTestsOutputPositive(
                pipelineContinuationToken,
                latestPipelineContinuationToken);
        }

        public sealed class PipelineContinuationTokenTestsInput : BaselineTestInput
        {
            public PipelineContinuationTokenTestsInput(
                string description,
                string continuationToken)
                : base(description)
            {
                this.ContinuationToken = continuationToken;
            }

            public string ContinuationToken { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteStartElement(nameof(this.ContinuationToken));
                xmlWriter.WriteCData(this.ContinuationToken.ToString());
                xmlWriter.WriteEndElement();
            }
        }

        public abstract class PipelineContinuationTokenTestsOutput : BaselineTestOutput
        {

        }

        public sealed class PipelineContinuationTokenTestsOutputPositive : PipelineContinuationTokenTestsOutput
        {
            internal PipelineContinuationTokenTestsOutputPositive(
                PipelineContinuationToken parsedToken,
                PipelineContinuationToken latestToken)
            {
                this.ParsedToken = parsedToken;
                this.LatestToken = latestToken;
            }

            internal PipelineContinuationToken ParsedToken { get; }
            internal PipelineContinuationToken LatestToken { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(this.ParsedToken));
                xmlWriter.WriteCData(this.ParsedToken.ToString());
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement(nameof(this.LatestToken));
                xmlWriter.WriteCData(this.LatestToken.ToString());
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class PipelineContinuationTokenTestsOutputNegative : PipelineContinuationTokenTestsOutput
        {
            public PipelineContinuationTokenTestsOutputNegative(string reason)
            {
                this.Reason = reason;
            }

            public string Reason { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(this.Reason));
                xmlWriter.WriteCData(this.Reason.ToString());
                xmlWriter.WriteEndElement();
            }
        }
    }
}