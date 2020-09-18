//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public sealed class CosmosDiagnosticsSerializerTests : BaselineTests<CosmosDiagnosticsSerializerBaselineInput, CosmosDiagnosticsSerializerBaselineOutput>
    {
        [TestMethod]
        public void TestPointOperationStatistics()
        {
            IList<CosmosDiagnosticsSerializerBaselineInput> inputs = new List<CosmosDiagnosticsSerializerBaselineInput>()
            {
                new CosmosDiagnosticsSerializerBaselineInput(
                    description: nameof(PointOperationStatistics),
                    diagnosticsInternal: new PointOperationStatistics(
                    activityId: Guid.Empty.ToString(),
                    responseTimeUtc: new DateTime(2020, 1, 2, 3, 4, 5, 6),
                    statusCode: System.Net.HttpStatusCode.OK,
                    subStatusCode: Documents.SubStatusCodes.WriteForbidden,
                    requestCharge: 4,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: "http://localhost.com",
                    requestSessionToken: nameof(PointOperationStatistics.RequestSessionToken),
                    responseSessionToken: nameof(PointOperationStatistics.ResponseSessionToken)))
            };

            this.ExecuteTestSuite(inputs);
        }

        public override CosmosDiagnosticsSerializerBaselineOutput ExecuteTest(CosmosDiagnosticsSerializerBaselineInput input)
        {
            return new CosmosDiagnosticsSerializerBaselineOutput(input.DiagnosticsInternal.ToString());
        }
    }

    public sealed class CosmosDiagnosticsSerializerBaselineInput : BaselineTestInput
    {
        internal CosmosDiagnosticsSerializerBaselineInput(string description, CosmosDiagnosticsInternal diagnosticsInternal)
            : base(description)
        {
            this.DiagnosticsInternal = diagnosticsInternal ?? throw new ArgumentNullException(nameof(diagnosticsInternal));
        }

        internal CosmosDiagnosticsInternal DiagnosticsInternal { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString(nameof(this.Description), this.Description);
            xmlWriter.WriteStartElement(nameof(this.DiagnosticsInternal));
            xmlWriter.WriteCData(JsonConvert.SerializeObject(this.DiagnosticsInternal, Newtonsoft.Json.Formatting.Indented));
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class CosmosDiagnosticsSerializerBaselineOutput : BaselineTestOutput
    {
        public CosmosDiagnosticsSerializerBaselineOutput(string toString)
        {
            this.ToStringOutput = toString ?? throw new ArgumentNullException(nameof(toString));
        }

        private string ToStringOutput { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.ToStringOutput));
            xmlWriter.WriteCData(this.ToStringOutput);
            xmlWriter.WriteEndElement();
        }
    }
}
