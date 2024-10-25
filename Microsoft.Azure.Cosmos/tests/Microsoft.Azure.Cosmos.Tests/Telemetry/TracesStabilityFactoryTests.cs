//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using global::Azure.Core;

    [TestClass]
    public class TracesStabilityFactoryTests
    {
        [TestMethod]
        [DataRow("database")]
        [DataRow("database/dup")]
        [DataRow(null)]
        public void SetAttributeTest(string stabilityMode)
        {
            Environment.SetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN", stabilityMode);

            DiagnosticScope scope = new DiagnosticScope();
            TracesStabilityFactory.SetAttributes(
                scope: scope,
                operationName: "operationName",
                databaseName: "databaseName",
                containerName: "containerName",
                accountName: "accountName",
                userAgent: "userAgent",
                machineId: "machineId",
                clientId: "clientId",
                connectionMode: "connectionMode");

            TracesStabilityFactory.SetAttributes(
                scope: scope,
                exception: new Exception());

            TracesStabilityFactory.SetAttributes(
                scope: scope,
                operationType: "operationType",
                queryTextMode: QueryTextMode.All,
                response: new Cosmos.Telemetry.OpenTelemetryAttributes());


        }
    }
}
