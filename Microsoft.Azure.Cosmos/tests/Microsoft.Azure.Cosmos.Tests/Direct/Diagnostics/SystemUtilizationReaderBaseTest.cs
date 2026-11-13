//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Direct
{
    using System;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SystemUtilizationReaderBaseTest
    {
        [TestMethod]
        public void GetSystemWideCpuUsageCached_ReusesAndRefreshesCachedValue()
        {
            TestReader reader = new TestReader { CpuUsage = 10 };
            Assert.AreEqual(10, reader.GetSystemWideCpuUsageCached(TimeSpan.Zero));
            Assert.AreEqual(1, reader.ReadCount);

            reader.CpuUsage = 25;
            Assert.AreEqual(10, reader.GetSystemWideCpuUsageCached(TimeSpan.MaxValue));
            Assert.AreEqual(1, reader.ReadCount);

            Assert.AreEqual(25, reader.GetSystemWideCpuUsageCached(TimeSpan.Zero));
            Assert.AreEqual(2, reader.ReadCount);
        }

        private sealed class TestReader : SystemUtilizationReaderBase
        {
            public float CpuUsage { get; set; }

            public int ReadCount { get; private set; }

            protected override float GetSystemWideCpuUsageCore()
            {
                this.ReadCount++;
                return this.CpuUsage;
            }

            protected override long? GetSystemWideMemoryAvailabiltyCore() => null;
        }
    }
}
