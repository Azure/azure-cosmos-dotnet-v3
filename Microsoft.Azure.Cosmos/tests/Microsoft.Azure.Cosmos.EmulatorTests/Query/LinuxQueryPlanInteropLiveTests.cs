//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using LegacyQueryTests = Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryTests;

    [TestClass]
    [TestCategory("Query")]
    public sealed class LinuxQueryPlanInteropLiveTests
    {
        [TestMethod]
        [TestCategory("LinuxQueryPlanInteropLive")]
        public async Task TestQueryUnicodeDocumentHttpsGateway()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.Inconclusive("Windows runs the original QueryTests Unicode test.");
            }

            // Reuse the assertions without QueryTests' account-wide setup/cleanup.
            await LegacyQueryTests.TestQueryUnicodeDocument(useGateway: true, protocol: Protocol.Https);
        }
    }
}
