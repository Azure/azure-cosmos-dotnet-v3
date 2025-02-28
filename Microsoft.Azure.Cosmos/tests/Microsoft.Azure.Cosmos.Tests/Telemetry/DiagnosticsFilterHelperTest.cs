//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Net;
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

            CosmosThresholdOptions distributedTracingOptions = new CosmosThresholdOptions
            {
                PointOperationLatencyThreshold = this.rootTrace.Duration.Add(TimeSpan.FromSeconds(1))
            };

            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.Accepted,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsFalse(
                DiagnosticsFilterHelper
                                .IsLatencyThresholdCrossed(distributedTracingOptions, OperationType.Read, response),
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Configured threshold value is {distributedTracingOptions.PointOperationLatencyThreshold.Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}");
        }

        [TestMethod]
        [DataRow("50", "60", false, DisplayName = "When Request and Response content length is less than threshold.")]
        [DataRow("150", "60", true, DisplayName = "When Request content length is greater than threshold but response content length is less than threshold.")]
        [DataRow("50", "160", true, DisplayName = "When Request content length is less than threshold but response content length is greater than threshold.")]
        [DataRow("150", "160", true, DisplayName = "When Request and Response content length is greater than threshold.")]
        [DataRow("Invalid Request Length", "160", true, DisplayName = "When Request content length is 'Invalid' and response content length is greater than threshold.")]
        [DataRow("Invalid Request Length", "60", false, DisplayName = "When Request content length is 'Invalid' and response content length is less than threshold.")]
        [DataRow("150", "Invalid Response Length", true, DisplayName = "When Request content length is greater than threshold and response content length is 'Invalid'.")]
        [DataRow("50", "Invalid Response Length", false, DisplayName = "When Request content length is less than threshold and response content length is 'invalid'.")]
        [DataRow(null, "160", true, DisplayName = "When Request content length is 'null' and response content length is greater than threshold.")]
        [DataRow(null, "60", false, DisplayName = "When Request content length is 'null' and response content length is less than threshold.")]
        [DataRow("150", null, true, DisplayName = "When Request content length is greater than threshold and response content length is 'null'.")]
        [DataRow("50", null, false, DisplayName = "When Request content length is less than threshold and response content length is 'null'.")]
        public void CheckReturnFalseOnSuccessAndLowerPayloadSizeThanConfiguredConfig(string requestContentLength, string responseContentLength, bool expectedResult)
        {
            CosmosThresholdOptions distributedTracingOptions = new CosmosThresholdOptions
            {
                PayloadSizeThresholdInBytes = 100
            };

            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                ResponseContentLength = requestContentLength,
                RequestContentLength = responseContentLength,
            };

            Assert.AreEqual(expectedResult,
                DiagnosticsFilterHelper
                                .IsPayloadSizeThresholdCrossed(distributedTracingOptions, response));
        }

        [TestMethod]
        public void CheckReturnTrueOnFailedStatusCode()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);
            OpenTelemetryAttributes response = new OpenTelemetryAttributes
            {
                StatusCode = HttpStatusCode.BadRequest,
                Diagnostics = new CosmosTraceDiagnostics(this.rootTrace)
            };

            Assert.IsTrue(
                !DiagnosticsFilterHelper
                    .IsSuccessfulResponse(response.StatusCode, response.SubStatusCode),
                $" Response time is {response.Diagnostics.GetClientElapsedTime().Milliseconds}ms " +
                $"and Is response Success : {response.StatusCode.IsSuccess()}");
        }

        [TestMethod]
        public void CheckedDefaultThresholdBasedOnOperationType()
        {
            Assert.IsTrue(this.rootTrace.Duration > TimeSpan.Zero);

            CosmosThresholdOptions config = new CosmosThresholdOptions();

            Array values = Enum.GetValues(typeof(OperationType));

            foreach (OperationType operationType in values)
            {
                TimeSpan defaultThreshold = DiagnosticsFilterHelper.DefaultLatencyThreshold(operationType, config);

                if (DiagnosticsFilterHelper.IsPointOperation(operationType))
                    Assert.AreEqual(defaultThreshold, config.PointOperationLatencyThreshold);
                else
                    Assert.AreEqual(defaultThreshold, config.NonPointOperationLatencyThreshold);
            }
        }

    }
}