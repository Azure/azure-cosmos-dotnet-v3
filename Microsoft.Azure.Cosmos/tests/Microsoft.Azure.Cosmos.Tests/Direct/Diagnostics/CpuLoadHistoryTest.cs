//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    [TestClass]
    public class CpuLoadHistoryTest
    {
        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadFalseWithNaN()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, float.NaN),
                new CpuLoad(now.AddMilliseconds(1), float.NaN),
                new CpuLoad(now.AddMilliseconds(2), float.NaN)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadTrueWithNaNOnTheBasisOfTime()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, float.NaN),
                new CpuLoad(now.AddMilliseconds(2), float.NaN),
                new CpuLoad(now.AddMilliseconds(4), float.NaN)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsTrue(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadFalseWithNaNAndValidValues()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 2),
                new CpuLoad(now.AddMilliseconds(1), float.NaN),
                new CpuLoad(now.AddMilliseconds(2), 10)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadTrueWithNaNAndValidValues()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 2),
                new CpuLoad(now.AddMilliseconds(1), float.NaN),
                new CpuLoad(now.AddMilliseconds(2), 91)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsTrue(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadFalseWithValidValues()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 2),
                new CpuLoad(now.AddMilliseconds(1), 5),
                new CpuLoad(now.AddMilliseconds(2), 10)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadTrueWithValidValues()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 2),
                new CpuLoad(now.AddMilliseconds(1), 5),
                new CpuLoad(now.AddMilliseconds(2), 91)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsTrue(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadTrueWithValidValuesOnTheBasisOfTime()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 2),
                new CpuLoad(now.AddMilliseconds(2), 5),
                new CpuLoad(now.AddMilliseconds(4), 90)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsTrue(cpuLoadHistory.IsCpuOverloaded);
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckOverloadTrueWithValidValuesGreaterThan100()
        {
            DateTime now = DateTime.Now;
            ReadOnlyCollection<CpuLoad> cpuLoad = new ReadOnlyCollection<CpuLoad>(new List<CpuLoad>
            {
                new CpuLoad(now, 150),
                new CpuLoad(now.AddMilliseconds(2), 5),
                new CpuLoad(now.AddMilliseconds(4), 90)
            });
            CpuLoadHistory cpuLoadHistory = new CpuLoadHistory(cpuLoad, TimeSpan.FromMilliseconds(1));

            Assert.IsTrue(cpuLoadHistory.IsCpuOverloaded);
        }
    }
}
