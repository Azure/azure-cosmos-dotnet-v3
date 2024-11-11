//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
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
    }
}