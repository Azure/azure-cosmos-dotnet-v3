//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public class DiagnosticHandlerHelperTests
    {
        [ClassInitialize]
        public static void Initialize(TestContext _)
        {
            DiagnosticHandlerHelperTests.ResetDiagnosticsHandlerHelper();
        }

        private static void ResetDiagnosticsHandlerHelper()
        {
            //Stop the job
            DiagnosticsHandlerHelper helper = DiagnosticsHandlerHelper.GetInstance();
            MethodInfo iMethod = helper.GetType().GetMethod("StopSystemMonitor", BindingFlags.NonPublic | BindingFlags.Instance);
            iMethod.Invoke(helper, new object[] { });

            //Reset the DiagnosticSystemUsageRecorder with original value
            FieldInfo DiagnosticSystemUsageRecorderField = typeof(DiagnosticsHandlerHelper).GetField("DiagnosticSystemUsageRecorder",
                            BindingFlags.Static |
                            BindingFlags.NonPublic);
            DiagnosticSystemUsageRecorderField.SetValue(null, new Documents.Rntbd.SystemUsageRecorder(
                            identifier: "diagnostic",
                            historyLength: 6,
                            refreshInterval: DiagnosticsHandlerHelper.DiagnosticsRefreshInterval));

            //Reset the instance with original value
            FieldInfo field = typeof(DiagnosticsHandlerHelper).GetField("Instance",
                            BindingFlags.Static |
                            BindingFlags.NonPublic);
            field.SetValue(null, Activator.CreateInstance(typeof(DiagnosticsHandlerHelper), true));

        }

        [TestMethod]
        public void SingletonTest()
        {
            DiagnosticsHandlerHelper diagnosticHandlerHelper1 = DiagnosticsHandlerHelper.GetInstance();
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();

            Assert.IsNotNull(diagnosticHandlerHelper1);
            Assert.IsNotNull(diagnosticHandlerHelper2);
            Assert.AreEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2, "Not Singleton");
        }

        [TestMethod]
        public async Task RefreshTestAsync()
        {
            // Get default instance of DiagnosticsHandlerHelper with client telemetry disabled (default)
            DiagnosticsHandlerHelper diagnosticHandlerHelper1 = DiagnosticsHandlerHelper.GetInstance();
            await Task.Delay(10000); // warm up to make sure there is at least one entry in the history
            Assert.IsNotNull(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper1.GetClientTelemetrySystemHistory());
            Assert.IsTrue(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count > 0);
            int countBeforeRefresh = diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count;

            FieldInfo TelemetrySystemUsageRecorderField1 = typeof(DiagnosticsHandlerHelper).GetField("TelemetrySystemUsageRecorder",
                            BindingFlags.Static |
                            BindingFlags.NonPublic);
            Assert.IsNull(TelemetrySystemUsageRecorderField1.GetValue(null));

            // Refresh instance of DiagnosticsHandlerHelper with client telemetry enabled
            DiagnosticsHandlerHelper.Refresh(isClientTelemetryEnabled: true);
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper2.GetDiagnosticsSystemHistory());
            int countAfterRefresh = diagnosticHandlerHelper2.GetDiagnosticsSystemHistory().Values.Count;

            Assert.IsTrue(countBeforeRefresh <= countAfterRefresh, "After Refresh count should be greater than or equal to before refresh count");
            Assert.AreNotEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2);

            await Task.Delay(5000); // warm up to make sure there is at least one entry in the history
            Assert.IsNotNull(diagnosticHandlerHelper2.GetClientTelemetrySystemHistory());

            Assert.IsTrue(diagnosticHandlerHelper2.GetClientTelemetrySystemHistory().Values.Count > 0);

            // Refresh instance of DiagnosticsHandlerHelper with client telemetry disabled
            DiagnosticsHandlerHelper.Refresh(isClientTelemetryEnabled: false);
            DiagnosticsHandlerHelper diagnosticHandlerHelper3 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper3.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper3.GetClientTelemetrySystemHistory());

            FieldInfo TelemetrySystemUsageRecorderField3 = typeof(DiagnosticsHandlerHelper).GetField("TelemetrySystemUsageRecorder",
                            BindingFlags.Static |
                            BindingFlags.NonPublic);
            Assert.IsNull(TelemetrySystemUsageRecorderField3.GetValue(null));
        }

        /// <summary>
        /// Regression test: the "System Info" (CPU/memory) datum must be attached to the request
        /// diagnostics on BOTH the success and the failure path. Previously it was only added after a
        /// successful inner SendAsync, so operations that failed by exception (timeouts, cancellations,
        /// exhausted retries) - exactly when CPU/memory is most useful - had no CPU in their diagnostics.
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task SystemInfoIsCapturedOnSuccessAndFailurePathsAsync()
        {
            // Ensure a clean, started monitor and wait until it has at least one system-usage sample so
            // GetDiagnosticsSystemHistory() returns non-null (mirrors the RefreshTestAsync warm-up).
            DiagnosticHandlerHelperTests.ResetDiagnosticsHandlerHelper();
            await DiagnosticHandlerHelperTests.WaitForDiagnosticsSystemHistoryAsync();

            // Success path: System Info must be present. This also guards against a false pass: if the
            // system history were unavailable, this assertion fails loudly instead of the test passing
            // trivially because neither path captured anything.
            using (ITrace successTrace = Tracing.Trace.GetRootTrace("SuccessRoot"))
            {
                RequestMessage successRequest = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"))
                {
                    Trace = successTrace,
                };
                DiagnosticsHandler successHandler = new DiagnosticsHandler
                {
                    InnerHandler = new TestHandler((request, token) => TestHandler.ReturnSuccess()),
                };

                await successHandler.SendAsync(successRequest, default);

                string successDiagnostics = new CosmosTraceDiagnostics(successTrace).ToString();
                Assert.IsTrue(
                    successDiagnostics.Contains("System Info"),
                    $"System Info should be present in the diagnostics of a successful operation. Diagnostics: {successDiagnostics}");
            }

            // Failure path: the inner handler throws (simulating a request timeout). Before the fix the
            // "System Info" attach was skipped on the exception path; after the fix it must be present.
            using (ITrace failureTrace = Tracing.Trace.GetRootTrace("FailureRoot"))
            {
                RequestMessage failureRequest = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"))
                {
                    Trace = failureTrace,
                };
                DiagnosticsHandler failureHandler = new DiagnosticsHandler
                {
                    InnerHandler = new TestHandler((request, token) => throw new OperationCanceledException("Simulated request timeout")),
                };

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => failureHandler.SendAsync(failureRequest, default),
                    "The simulated failure should propagate to the caller unchanged.");

                string failureDiagnostics = new CosmosTraceDiagnostics(failureTrace).ToString();
                Assert.IsTrue(
                    failureDiagnostics.Contains("System Info"),
                    $"System Info should be present in the diagnostics even when the operation fails. Diagnostics: {failureDiagnostics}");
            }
        }

        /// <summary>
        /// End-to-end regression test for the real customer scenario: an operation that ends in a
        /// <see cref="CosmosOperationCanceledException"/> (request timeout / cancellation). That exception is
        /// created by the retry handler - which sits BELOW the DiagnosticsHandler - and captures the trace
        /// lazily. This verifies the System Info attached in the DiagnosticsHandler's finally (during unwind)
        /// is included in the exception's own Diagnostics, not just in a directly-serialized live trace.
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task SystemInfoIsCapturedWhenOperationIsCanceledAsync()
        {
            DiagnosticHandlerHelperTests.ResetDiagnosticsHandlerHelper();
            await DiagnosticHandlerHelperTests.WaitForDiagnosticsSystemHistoryAsync();

            // The inner handler mimics AbstractRetryHandler: on cancellation it captures the trace into a
            // CosmosOperationCanceledException at an inner layer, exactly as the real pipeline does. The
            // exception is therefore created BEFORE the DiagnosticsHandler's finally attaches System Info.
            DiagnosticsHandler handler = new DiagnosticsHandler
            {
                InnerHandler = new TestHandler((request, token) =>
                    throw new CosmosOperationCanceledException(
                        new OperationCanceledException("Simulated request timeout"),
                        request.Trace)),
            };

            using (ITrace rootTrace = Tracing.Trace.GetRootTrace("CancelRoot"))
            {
                RequestMessage request = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"))
                {
                    Trace = rootTrace,
                };

                CosmosOperationCanceledException caught = await Assert.ThrowsExceptionAsync<CosmosOperationCanceledException>(
                    () => handler.SendAsync(request, default),
                    "The CosmosOperationCanceledException should propagate to the caller unchanged.");

                // The exception's own diagnostics (what the customer sees) must contain System Info.
                string diagnostics = caught.Diagnostics.ToString();
                Assert.IsTrue(
                    diagnostics.Contains("System Info"),
                    $"System Info should be present in the diagnostics of a canceled operation. Diagnostics: {diagnostics}");
            }
        }

        private static async Task WaitForDiagnosticsSystemHistoryAsync()
        {
            DiagnosticsHandlerHelper helper = DiagnosticsHandlerHelper.GetInstance();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // The monitor samples on DiagnosticsRefreshInterval (10s); poll generously (well above the
            // interval) so the first sample is available even on a slow/loaded CI agent. If it never
            // arrives we fail loudly below rather than letting the test pass without validating capture.
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(45))
            {
                Documents.Rntbd.SystemUsageHistory history = helper.GetDiagnosticsSystemHistory();
                if (history != null && history.Values.Count > 0)
                {
                    return;
                }

                await Task.Delay(500);
            }

            Assert.Fail("Diagnostics system usage history did not become available in time; cannot validate System Info capture.");
        }
    }
}