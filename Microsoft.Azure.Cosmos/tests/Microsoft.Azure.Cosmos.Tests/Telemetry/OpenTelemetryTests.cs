//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class OpenTelemetryTests
    {
        private static ClientDiagnosticListener testListener;

        public static void ClassInitialize()
        {
            OpenTelemetryTests.testListener = new ClientDiagnosticListener("Azure.Cosmos");
        }

        public static void FinalCleanup()
        {
            OpenTelemetryTests.testListener.Dispose();
        }
    }
}