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
        public void CheckReturnFalseOnSuccessAndLowerLatencyThanConfiguredConfig()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);

            DistributedTracingOptions distributedTracingOptions = new DistributedTracingOptions
            {
                LatencyThresholdForDiagnosticEvent = this.rootTrace.Duration.Add(TimeSpan.FromSeconds(1))
            };
            
            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.Accepted,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsFalse(
                DiagnosticsFilterHelper
                                .IsTracingNeeded(distributedTracingOptions, OperationType.Read, response), 
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {distributedTracingOptions.LatencyThresholdForDiagnosticEvent.Value.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}" );
        }

        [TestMethod]
        public void CheckReturnTrueOnFailedStatusCode()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);


            DistributedTracingOptions distributedTracingOptions = new DistributedTracingOptions
            {
                LatencyThresholdForDiagnosticEvent = this.rootTrace.Duration.Add(TimeSpan.FromSeconds(1))
            };

            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.BadRequest,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsTrue(
                DiagnosticsFilterHelper
                    .IsTracingNeeded(distributedTracingOptions, OperationType.Read, response),
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {distributedTracingOptions.LatencyThresholdForDiagnosticEvent.Value.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}");

        }

    }
}
