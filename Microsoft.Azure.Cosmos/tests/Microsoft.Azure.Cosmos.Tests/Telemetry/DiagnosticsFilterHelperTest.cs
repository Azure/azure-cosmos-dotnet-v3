//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.Telemetry;
    using Cosmos.Telemetry.Diagnostics;
    using Cosmos.Tracing;
    using Diagnostics;
    using Documents;
    using VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class DiagnosticsFilterHelperTest
    {
        private Trace rootTrace;
        [TestInitialize]
        public void Initialize()
        {
            using (this.rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                Task.Delay(1).Wait();
            }
        }

        [TestMethod]
        public void CheckReturnFalseOnSuccessAndLowerLatencyThanClientOptionConfig()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);

            CosmosClientOptions clientOptions = new CosmosClientOptions { LatencyThresholdForDiagnosticsOnDistributingTracing = TimeSpan.FromMilliseconds(20) };
            RequestOptions requestOptions = new RequestOptions();
            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.Accepted,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsFalse(
                DiagnosticsFilterHelper
                                .HasIssueWithOperation(new OpenTelemetryOptions(clientOptions, requestOptions), response), 
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {clientOptions.LatencyThresholdForDiagnosticsOnDistributingTracing.Value.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}" );
        }


        [TestMethod]
        public void CheckReturnFalseOnSuccessAndLowerLatencyThanRequestOptionConfig()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);

            CosmosClientOptions clientOptions = new CosmosClientOptions();
            RequestOptions requestOptions = new RequestOptions { LatencyThresholdForDiagnosticsOnOTelTracer = TimeSpan.FromMilliseconds(20) };
            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.Accepted,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsFalse(
                DiagnosticsFilterHelper
                    .HasIssueWithOperation(new OpenTelemetryOptions(clientOptions, requestOptions), response),
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {requestOptions.LatencyThresholdForDiagnosticsOnOTelTracer.Value.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}");

        }

        [TestMethod]
        public void CheckClientOptionAndRequestOptionValuesAreOverriding()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions { LatencyThresholdForDiagnosticsOnDistributingTracing = TimeSpan.FromMilliseconds(10) };
            RequestOptions requestOptions = new RequestOptions { LatencyThresholdForDiagnosticsOnOTelTracer = TimeSpan.FromMilliseconds(20) };

            OpenTelemetryOptions openTelemetryOptions = new OpenTelemetryOptions(clientOptions, requestOptions);

            Assert.IsNotNull(openTelemetryOptions.LatencyThreshold);
            Assert.AreEqual(requestOptions.LatencyThresholdForDiagnosticsOnOTelTracer,openTelemetryOptions.LatencyThreshold,
                $" Overridden threshold value is {openTelemetryOptions.LatencyThreshold.Value.Milliseconds}ms ");

        }

        [TestMethod]
        public void CheckReturnTrueOnFailedStatusCode()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);

            CosmosClientOptions clientOptions = new CosmosClientOptions();
            RequestOptions requestOptions = new RequestOptions { LatencyThresholdForDiagnosticsOnOTelTracer = TimeSpan.FromMilliseconds(20) };
            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.BadRequest,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsTrue(
                DiagnosticsFilterHelper
                    .HasIssueWithOperation(new OpenTelemetryOptions(clientOptions, requestOptions), response),
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {requestOptions.LatencyThresholdForDiagnosticsOnOTelTracer.Value.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}");

        }

    }
}
