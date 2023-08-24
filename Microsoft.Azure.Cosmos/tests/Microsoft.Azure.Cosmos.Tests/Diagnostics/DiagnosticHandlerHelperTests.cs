//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DiagnosticHandlerHelperTests
    {
        [TestMethod]
        public void SingletonTest()
        {
            DiagnosticsHandlerHelper diagnosticHandlerHelper1 = DiagnosticsHandlerHelper.GetInstance();
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();

            Assert.AreEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2, "Not Singleton");
        }

        [TestMethod]
        public async Task RefreshTestAsync()
        {
            DiagnosticsHandlerHelper diagnosticHandlerHelper1 = DiagnosticsHandlerHelper.GetInstance();
            await Task.Delay(1000); // Give it some time to warm up
            Assert.IsTrue(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count < 2, $"Actual Count is {diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count}, expected count is less than 2"); // Making sure we collected less than to records
            await Task.Delay(10000);
            Assert.IsNotNull(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory());
            Assert.IsTrue(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count == 2, $"Actual Count is {diagnosticHandlerHelper1.GetDiagnosticsSystemHistory().Values.Count}, expected count is 2"); // After 10 of wait, there must be 2 records (collecting every 10 sec)

            DiagnosticsHandlerHelper.Refresh(true);
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();
            await Task.Delay(1000); // Give it some time to warm up

            Assert.AreNotEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2);
            Assert.IsNotNull(diagnosticHandlerHelper2.GetDiagnosticsSystemHistory());
            Assert.IsTrue(diagnosticHandlerHelper2.GetDiagnosticsSystemHistory().Values.Count == 2, $"Actual Count is {diagnosticHandlerHelper2.GetDiagnosticsSystemHistory().Values.Count}, expected count is 2"); // Making sure after refresh we are not loosing old data.
            Assert.IsNotNull(diagnosticHandlerHelper2.GetClientTelemetrySystemHistory());

            DiagnosticsHandlerHelper.Refresh(false);
            DiagnosticsHandlerHelper diagnosticHandlerHelper3 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper3.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper3.GetClientTelemetrySystemHistory());
        }
    }
}
