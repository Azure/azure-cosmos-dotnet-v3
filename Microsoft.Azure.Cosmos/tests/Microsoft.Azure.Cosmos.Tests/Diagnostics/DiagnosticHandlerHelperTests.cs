//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
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

            Assert.AreEqual(diagnosticHandlerHelper1, diagnosticHandlerHelper2);
        }

        [TestMethod]
        public async Task RefreshTestAsync()
        {
            DiagnosticsHandlerHelper diagnosticHandlerHelper1 = DiagnosticsHandlerHelper.GetInstance();
            await Task.Delay(1000);
            Assert.IsNotNull(diagnosticHandlerHelper1.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper1.GetClientTelemetrySystemHistory());

            DiagnosticsHandlerHelper.Refresh(true);
            await Task.Delay(1000);
            DiagnosticsHandlerHelper diagnosticHandlerHelper2 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper2.GetDiagnosticsSystemHistory());
            Assert.IsNotNull(diagnosticHandlerHelper2.GetClientTelemetrySystemHistory());

            DiagnosticsHandlerHelper.Refresh(false);
            await Task.Delay(1000);
            DiagnosticsHandlerHelper diagnosticHandlerHelper3 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper3.GetDiagnosticsSystemHistory());
            Assert.IsNull(diagnosticHandlerHelper3.GetClientTelemetrySystemHistory());

            DiagnosticsHandlerHelper.Refresh(true);
            await Task.Delay(1000);
            DiagnosticsHandlerHelper diagnosticHandlerHelper4 = DiagnosticsHandlerHelper.GetInstance();
            Assert.IsNotNull(diagnosticHandlerHelper4.GetDiagnosticsSystemHistory());
            Assert.IsNotNull(diagnosticHandlerHelper4.GetClientTelemetrySystemHistory());
        }
    }
}
