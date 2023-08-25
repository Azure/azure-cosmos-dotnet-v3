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
        [TestInitialize]
        public void Initialize()
        {
            //Stop the job
            DiagnosticsHandlerHelper helper = DiagnosticsHandlerHelper.GetInstance();
            MethodInfo iMethod= helper.GetType().GetMethod("StopSystemMonitor", BindingFlags.NonPublic | BindingFlags.Instance);
            iMethod.Invoke(helper, new object[] { });

            //Reset the instance woth original value
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
            await Task.Delay(10000); // warm up
            Assert.IsNotNull(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory());
            int countBeforeRefresh = diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count;

            // Refresh instance of DiagnosticsHandlerHelper with client telemetry enabled
            DiagnosticsHandlerHelper.Refresh(true);
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();
            int countAfterRefresh = diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count;

            Console.WriteLine(countBeforeRefresh + " " + countAfterRefresh);
            Assert.IsTrue(countBeforeRefresh <= countAfterRefresh, "After Refresh count should be greater than or equal to before refresh count");

            Assert.AreNotEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2);

            Assert.IsNotNull(diagnosticHandlerHelper2.GetDiagnosticsSystemHistory());
            Assert.IsNotNull(diagnosticHandlerHelper2.GetClientTelemetrySystemHistory());

            // Refresh instance of DiagnosticsHandlerHelper with client telemetry disabled
            DiagnosticsHandlerHelper.Refresh(false);
            DiagnosticsHandlerHelper diagnosticHandlerHelper3 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper3.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper3.GetClientTelemetrySystemHistory());
        }
    }
}
