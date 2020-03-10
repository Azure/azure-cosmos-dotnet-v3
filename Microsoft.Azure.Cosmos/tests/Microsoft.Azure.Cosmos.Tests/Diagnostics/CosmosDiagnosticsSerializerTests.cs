//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using System.Xml;
    using System.Collections.Generic;
    using System;
    using Newtonsoft.Json;
    using System.Net.Http;

    [TestClass]
    public sealed class CosmosDiagnosticsSerializerTests : BaselineTests<CosmosDiagnosticsSerializerBaselineInput, CosmosDiagnosticsSerializerBaselineOutput>
    {
        [TestMethod]
        public void TestPointOperationStatistics()
        {
            CosmosDiagnosticsContext context = new CosmosDiagnosticsContextCore(null);
            context.AddDiagnosticsInternal(new PointOperationStatistics(
                    activityId: Guid.Empty.ToString(),
                    statusCode: System.Net.HttpStatusCode.OK,
                    subStatusCode: Documents.SubStatusCodes.WriteForbidden,
                    requestCharge: 4,
                    errorMessage: null,
                    method: HttpMethod.Post,
                    requestUri: new Uri("http://localhost.com"),
                    requestSessionToken: nameof(PointOperationStatistics.RequestSessionToken),
                    responseSessionToken: nameof(PointOperationStatistics.ResponseSessionToken),
                    clientSideRequestStatistics: null));

            IList<CosmosDiagnosticsSerializerBaselineInput> inputs = new List<CosmosDiagnosticsSerializerBaselineInput>()
            {
                new CosmosDiagnosticsSerializerBaselineInput(
                    description: nameof(PointOperationStatistics),
                    cosmosDiagnostics: context.Diagnostics)
            };

            this.ExecuteTestSuite(inputs);
        }

        public override CosmosDiagnosticsSerializerBaselineOutput ExecuteTest(CosmosDiagnosticsSerializerBaselineInput input)
        {
            return new CosmosDiagnosticsSerializerBaselineOutput(input.CosmosDiagnostics.ToString());
        }
    }

    public sealed class CosmosDiagnosticsSerializerBaselineInput : BaselineTestInput
    {
        public CosmosDiagnosticsSerializerBaselineInput(string description, CosmosDiagnostics cosmosDiagnostics)
            : base(description)
        {
            this.CosmosDiagnostics = cosmosDiagnostics ?? throw new ArgumentNullException(nameof(cosmosDiagnostics));
        }

        public CosmosDiagnostics CosmosDiagnostics { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString(nameof(this.Description), this.Description);
            xmlWriter.WriteStartElement(nameof(this.CosmosDiagnostics));
            xmlWriter.WriteCData(JsonConvert.SerializeObject(this.CosmosDiagnostics, Newtonsoft.Json.Formatting.Indented));
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
