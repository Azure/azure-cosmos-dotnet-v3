//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PipelineContinuationTokenTests : BaselineTests<PipelineContinuationTokenTests.PipelineContinuationTokenTestsInput, PipelineContinuationTokenTests.PipelineContinuationTokenTestsOutput>
    {
        [TestMethod]
        [Owner("brchon")]
        public void Tests()
        {
            {
                string versionZeroContinuation = @"{""asdf"" : ""asdf""}";
                Assert.IsTrue(PipelineContinuationToken.TryParse(
                    versionZeroContinuation,
                    out PipelineContinuationToken pipelinedContinuationTokenVersionZero));
                Assert.AreEqual(
                    PipelineContinuationTokenV0.Version0,
                    pipelinedContinuationTokenVersionZero.Version);
                PipelineContinuationTokenV0 pipelinedContinuationTokenVersionZeroCasted = (PipelineContinuationTokenV0)pipelinedContinuationTokenVersionZero;
                Assert.AreEqual(
                    versionZeroContinuation,
                    pipelinedContinuationTokenVersionZeroCasted.SourceContinuationToken);
                Assert.IsTrue(PipelineContinuationToken.TryConvertToLatest(
                    pipelinedContinuationTokenVersionZero,
                    out PipelineContinuationTokenV2 converted));
                Assert.AreEqual(null, converted.QueryPlan);
                Assert.AreEqual(versionZeroContinuation, converted.SourceContinuationToken);
            }
        }

        public override PipelineContinuationTokenTestsOutput ExecuteTest(
            PipelineContinuationTokenTestsInput input)
        {
            Assert.IsTrue(PipelineContinuationToken.TryParse(
                input.ContinuationToken,
                out PipelineContinuationToken pipelineContinuationToken));
            Assert.IsTrue(PipelineContinuationToken.TryConvertToLatest(
                pipelineContinuationToken,
                out PipelineContinuationTokenV2 latestPipelineContinuationToken));
            PipelineContinuationTokenTestsOutput output = new PipelineContinuationTokenTestsOutput(
                pipelineContinuationToken,
                latestPipelineContinuationToken);

            return output;
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
            }
        }

        public sealed class PipelineContinuationTokenTestsOutput : BaselineTestOutput
        {
            internal PipelineContinuationTokenTestsOutput(
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
                xmlWriter.WriteElementString(nameof(this.ParsedToken), this.ParsedToken.ToString());
                xmlWriter.WriteElementString(nameof(this.LatestToken), this.ParsedToken.ToString());
            }
        }
    }
}
