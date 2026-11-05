//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.InMemoryEmulatorTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EmulatorLifecycleTests
    {
        [TestMethod]
        public void ReadsDynamicallyAssignedEndpoints()
        {
            (Uri account, Uri management) = InMemoryEmulator.ParseReadyRecord(
                "{\"event\":\"ready\",\"accountEndpoint\":\"http://127.0.0.1:49151/\",\"managementEndpoint\":\"http://127.0.0.1:49150/\"}");
            Assert.AreEqual(49151, account.Port);
            Assert.AreEqual(49150, management.Port);
        }

        [DataTestMethod]
        [DataRow("https://127.0.0.1:8081/")]
        [DataRow("http://example.com:8081/")]
        [DataRow("http://127.0.0.1:8081/dbs")]
        [DataRow("http://user@127.0.0.1:8081/")]
        public void RejectsUnexpectedEndpoints(string endpoint)
        {
            Assert.ThrowsException<InvalidOperationException>(() => InMemoryEmulator.ParseReadyRecord(
                $"{{\"event\":\"ready\",\"accountEndpoint\":\"{endpoint}\",\"managementEndpoint\":\"http://127.0.0.1:49150/\"}}"));
        }

        [TestMethod]
        public void ReportsExitBeforeReadiness()
        {
            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => InMemoryEmulator.ParseReadyRecord(null));
            StringAssert.Contains(error.Message, "exited before");
        }

        [TestMethod]
        public async Task MissingExecutableIsAnError()
        {
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => InMemoryEmulator.StartAsync(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), "emulator-config.json"));
        }
    }
}