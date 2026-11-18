//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for <see cref="CpuReaderBase"/>  scenarios.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CpuMonitorTests
    {
        [TestMethod]
        [Timeout(30000)]
        [Owner("fabianm")]
        public async Task CpuMonitorWithWindowsCpuReader()
        {
            try
            {
                CpuMonitor.OverrideRefreshInterval(newRefreshInterval: TimeSpan.FromMilliseconds(100));
                using (CpuMonitor monitor = new CpuMonitor())
                {
                    monitor.Start();

                    Stopwatch watch = Stopwatch.StartNew();
                    CpuLoadHistory history = null;
                    // burning some CPU
                    while ((watch.ElapsedMilliseconds < 500 || (history = monitor.GetCpuLoad()) == null)
                        && watch.Elapsed < TimeSpan.FromSeconds(10))
                    {
                        if (watch.ElapsedMilliseconds > 500)
                        {
                            // adding a delay after burning CPU for 500ms
                            // the CPU measurement happens in a background thread
                            // with 100 CPU usage generated here, executing that one
                            // could take a while - the delay here makes that easier
                            await Task.Delay(200);
                        }
                    }
                    watch.Stop();

                    Assert.IsNotNull(history);
                    Assert.AreNotEqual("empty", history.ToString());
                    monitor.Stop();
                }
            }
            finally
            {
                ResetCpuMonitorRefreshInterval();
            }
        }

        [TestMethod]
        [Owner("fabianm")]
        public void CpuMonitorWithUnsupportedCpuReader()
        {
            try
            {
                SystemUtilizationReaderBase.ApplySingletonOverride(new UnsupportedSystemUtilizationReader());
                CpuMonitor.OverrideRefreshInterval(newRefreshInterval: TimeSpan.FromMilliseconds(100));
                using (CpuMonitor monitor = new CpuMonitor())
                {
                    monitor.Start();

                    Stopwatch watch = Stopwatch.StartNew();
                    // burning some CPU
                    while (watch.ElapsedMilliseconds < 500)
                    { }
                    watch.Stop();

                    CpuLoadHistory history = monitor.GetCpuLoad();
                    Assert.IsNull(history);

                    monitor.Stop();
                }
            }
            finally
            {
                ResetCpuMonitorRefreshInterval();
                SystemUtilizationReaderBase.ApplySingletonOverride(readerOverride: null);
            }
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void SystemUsageMonitorWithWindowsCpuAndMemoryReader()
        {
            SystemUsageHistory test1History = null;
            SystemUsageHistory test2History = null;

            using (SystemUsageMonitor monitor = SystemUsageMonitor.CreateAndStart(
                new List<SystemUsageRecorder> {
                    new SystemUsageRecorder("test1", 6, TimeSpan.FromMilliseconds(100)),
                    new SystemUsageRecorder("test2", 8, TimeSpan.FromMilliseconds(133))
                }))
            {
                // Wait for the monitor to start Recording
                Assert.IsTrue(SpinWait.SpinUntil(monitor.IsRunning, TimeSpan.FromSeconds(10)));

                Task.Delay(2000).Wait();

                Assert.IsNotNull(monitor);
                Assert.AreEqual(1, monitor.PollDelayInMs);

                SystemUsageRecorder r1 = monitor.GetRecorder("test1");
                test1History = r1.Data;

                SystemUsageRecorder r2 = monitor.GetRecorder("test2");
                test2History = r2.Data;

                Assert.IsFalse(monitor.TryGetBackgroundTaskException(out AggregateException aggregateException), aggregateException?.ToString());
                monitor.Stop();
            }

            Assert.IsNotNull(test1History);
            Assert.AreNotEqual("empty", test1History.ToString());
            Assert.IsTrue(test1History.Values.Count > 0);
            foreach (SystemUsageLoad load in test1History.Values)
            {
                Assert.IsNotNull(load.CpuUsage);
                Assert.IsFalse(Single.IsNaN(load.CpuUsage.Value));
                Assert.IsNotNull(load.MemoryAvailable);
                Assert.IsNotNull(load.ThreadInfo);
                Assert.IsNotNull(load.ThreadInfo.IsThreadStarving);
                Assert.IsNotNull(load.ThreadInfo.ThreadWaitIntervalInMs);

#if NETSTANDARD15 || NETSTANDARD16
                Assert.IsNull(load.ThreadInfo.AvailableThreads);
                Assert.IsNull(load.ThreadInfo.MinThreads);
                Assert.IsNull(load.ThreadInfo.MaxThreads);
#else
                Assert.IsNotNull(load.ThreadInfo.AvailableThreads);
                Assert.IsNotNull(load.ThreadInfo.MinThreads);
                Assert.IsNotNull(load.ThreadInfo.MaxThreads);
#endif
            }

            Assert.IsNotNull(test2History);
            Assert.AreNotEqual("empty", test2History.ToString());
            Assert.IsTrue(test2History.Values.Count > 0);
            foreach (SystemUsageLoad load in test2History.Values)
            {
                Assert.IsNotNull(load.CpuUsage);
                Assert.IsFalse(Single.IsNaN(load.CpuUsage.Value));
                Assert.IsNotNull(load.MemoryAvailable);
                Assert.IsNotNull(load.ThreadInfo);
                Assert.IsNotNull(load.ThreadInfo.IsThreadStarving);
                Assert.IsNotNull(load.ThreadInfo.ThreadWaitIntervalInMs);

#if NETSTANDARD15 || NETSTANDARD16
                Assert.IsNull(load.ThreadInfo.AvailableThreads);
                Assert.IsNull(load.ThreadInfo.MinThreads);
                Assert.IsNull(load.ThreadInfo.MaxThreads);
#else
                Assert.IsNotNull(load.ThreadInfo.AvailableThreads);
                Assert.IsNotNull(load.ThreadInfo.MinThreads);
                Assert.IsNotNull(load.ThreadInfo.MaxThreads);
#endif
            }
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckForDelayWithMutipleRecordersWithCommonDifference()
        {
            using (SystemUsageMonitor monitor = SystemUsageMonitor.CreateAndStart(new List<SystemUsageRecorder> {
                    new SystemUsageRecorder("test1", 6, TimeSpan.FromMilliseconds(10)),
                    new SystemUsageRecorder("test2", 8, TimeSpan.FromMilliseconds(20)),
                    new SystemUsageRecorder("test3", 8, TimeSpan.FromMilliseconds(30)),
                    new SystemUsageRecorder("test4", 8, TimeSpan.FromMilliseconds(40)) }))
            {
                Assert.IsNotNull(monitor);
                Assert.AreEqual(10, monitor.PollDelayInMs);
                Assert.IsFalse(monitor.TryGetBackgroundTaskException(out AggregateException aggregateException), aggregateException?.ToString());

                monitor.Stop();
            }
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void CheckForDelayWithMutipleRecordersRandomly()
        {
            using (SystemUsageMonitor monitor = SystemUsageMonitor.CreateAndStart(new List<SystemUsageRecorder> {
                    new SystemUsageRecorder("test1", 6, TimeSpan.FromMilliseconds(4)),
                    new SystemUsageRecorder("test2", 8, TimeSpan.FromMilliseconds(10)),
                    new SystemUsageRecorder("test3", 8, TimeSpan.FromMilliseconds(13)),
                    new SystemUsageRecorder("test4", 8, TimeSpan.FromMilliseconds(20)) }))
            {
                Assert.IsNotNull(monitor);
                Assert.AreEqual(1, monitor.PollDelayInMs);
                Assert.IsTrue(monitor is SystemUsageMonitor);
                Assert.IsFalse(monitor.TryGetBackgroundTaskException(out AggregateException aggregateException), aggregateException?.ToString());

                monitor.Stop();
            }
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        public void SystemUsageMonitorWithUnsupportedCpuAndMemoryReader()
        {
            SystemUsageRecorder r1, r2;
            SystemUsageMonitor monitor = null;

            SystemUsageHistory test1History = null;
            SystemUsageHistory test2History = null;

            try
            {
                SystemUtilizationReaderBase.ApplySingletonOverride(new UnsupportedSystemUtilizationReader());

                using (monitor = SystemUsageMonitor.CreateAndStart(new List<SystemUsageRecorder> {
                    new SystemUsageRecorder("test1", 6, TimeSpan.FromMilliseconds(1)),
                    new SystemUsageRecorder("test2", 8, TimeSpan.FromMilliseconds(2))
                }))
                {
                    Task.Delay(500).Wait();

                    r1 = monitor.GetRecorder("test1");
                    r2 = monitor.GetRecorder("test2");

                    Assert.IsNotNull(monitor);
                    Assert.IsTrue(monitor is SystemUsageMonitor);
                    Assert.AreEqual(1, monitor.PollDelayInMs);

                    test1History = r1.Data;
                    test2History = r2.Data;

                    Assert.IsFalse(monitor.TryGetBackgroundTaskException(out AggregateException aggregateException), aggregateException?.ToString());
                    monitor.Stop();
                }
                StringBuilder stringBuilder = new StringBuilder();
                Assert.IsNotNull(test1History);
                Assert.AreNotEqual("empty", test1History.ToString());
                Assert.IsTrue(test1History.Values.Count > 0);
                foreach (SystemUsageLoad load in test1History.Values)
                {
                    Assert.IsTrue(Single.IsNaN(load.CpuUsage.Value));
                    Assert.IsNull(load.MemoryAvailable);
                    Assert.IsNotNull(load.ThreadInfo);
                    Assert.IsNotNull(load.ThreadInfo.IsThreadStarving);
                    Assert.IsNotNull(load.ThreadInfo.ThreadWaitIntervalInMs);
                    load.AppendJsonString(stringBuilder);

#if NETSTANDARD15 || NETSTANDARD16
                    Assert.IsNull(load.ThreadInfo.AvailableThreads);
                    Assert.IsNull(load.ThreadInfo.MinThreads);
                    Assert.IsNull(load.ThreadInfo.MaxThreads);

#else
                    Assert.IsNotNull(load.ThreadInfo.AvailableThreads);
                    Assert.IsNotNull(load.ThreadInfo.MinThreads);
                    Assert.IsNotNull(load.ThreadInfo.MaxThreads);
#endif
                }
                Assert.IsTrue(stringBuilder.ToString().Contains("\"cpu\":\"no info\""));
                Assert.IsNotNull(test2History);
                Assert.AreNotEqual("empty", test2History.ToString());
                Assert.IsTrue(test2History.Values.Count > 0);
                foreach (SystemUsageLoad load in test2History.Values)
                {
                    Assert.IsTrue(Single.IsNaN(load.CpuUsage.Value));
                    Assert.IsNull(load.MemoryAvailable);
                    Assert.IsNotNull(load.ThreadInfo);
                    Assert.IsNotNull(load.ThreadInfo.IsThreadStarving);
                    Assert.IsNotNull(load.ThreadInfo.ThreadWaitIntervalInMs);

#if NETSTANDARD15 || NETSTANDARD16
                    Assert.IsNull(load.ThreadInfo.AvailableThreads);
                    Assert.IsNull(load.ThreadInfo.MinThreads);
                    Assert.IsNull(load.ThreadInfo.MaxThreads);
#else
                    Assert.IsNotNull(load.ThreadInfo.AvailableThreads);
                    Assert.IsNotNull(load.ThreadInfo.MinThreads);
                    Assert.IsNotNull(load.ThreadInfo.MaxThreads);
#endif
                }
            }
            finally
            {
                SystemUtilizationReaderBase.ApplySingletonOverride(null);
            }
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [Timeout(30000)]
        public void TestSystemUsageLoadString()
        {
            bool retry = false;
            Stopwatch timeout = Stopwatch.StartNew();
            do
            {
                Assert.IsTrue(timeout.Elapsed < TimeSpan.FromSeconds(10), "Thread information was not available within the timeout.");
                SystemUsageLoad information = new SystemUsageLoad(
                    DateTime.MinValue,
                    ThreadInformation.Get(),
                    90,
                    4200,
                    10);

                StringBuilder builder = new StringBuilder();
                information.AppendJsonString(builder);
                JObject jObject = JObject.Parse(builder.ToString());

                Assert.AreEqual(DateTime.MinValue, (DateTime)jObject["dateUtc"]);
                Assert.AreEqual(jObject["cpu"].ToString(), "90");
                Assert.AreEqual(jObject["memory"].ToString(), "4200");
                Assert.AreEqual(jObject["numberOfOpenTcpConnection"].ToString(), "10");

                JToken threadInfo = jObject["threadInfo"];
                Assert.IsNotNull(threadInfo);
                Assert.IsNotNull(threadInfo["isThreadStarving"].ToString());

                // Loop until the thread info is available.
                if (threadInfo["threadWaitIntervalInMs"] == null)
                {
                    retry = true;
                }
                else
                {
                    Assert.IsNotNull(threadInfo["threadWaitIntervalInMs"].ToString());
                    retry = false;
                }

#if !(NETSTANDARD15 || NETSTANDARD16)
                Assert.IsNotNull(threadInfo["availableThreads"].ToString());
                Assert.IsNotNull(threadInfo["minThreads"].ToString());
                Assert.IsNotNull(threadInfo["maxThreads"].ToString());
#endif
                if (retry)
                {
                    Thread.Sleep(1);
                }
            } while (retry);
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public void TestSystemUsageLoadWithNullCpuAndMemoryString()
        {
            SystemUsageLoad information = new SystemUsageLoad(
                DateTime.MinValue,
                ThreadInformation.Get(),
                null,
                null,
                null);

            StringBuilder builder = new StringBuilder();
            information.AppendJsonString(builder);
            JObject jObject = JObject.Parse(builder.ToString());

            Assert.AreEqual(DateTime.MinValue, (DateTime)jObject["dateUtc"]);
            Assert.AreEqual(jObject["cpu"].ToString(), "no info");
            Assert.AreEqual(jObject["memory"].ToString(), "no info");
            Assert.AreEqual(jObject["numberOfOpenTcpConnection"].ToString(), "no info");
        }

        [TestMethod]
        [Owner("aavasthy")]
        [Timeout(30000)]
        public void TestSystemUsageLoadWithVariantCulture()
        {
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("ru-RU");

                bool retry = false;
                Stopwatch timeout = Stopwatch.StartNew();
                do
                {
                    Assert.IsTrue(timeout.Elapsed < TimeSpan.FromSeconds(10), "Thread information was not available within the timeout.");
                    SystemUsageLoad information = new SystemUsageLoad(
                    DateTime.MinValue,
                    ThreadInformation.Get(),
                    90,
                    4200,
                    10);

                    StringBuilder builder = new StringBuilder();
                    information.AppendJsonString(builder);
                    JObject jObject = JObject.Parse(builder.ToString());

                    Assert.AreEqual(DateTime.MinValue, (DateTime)jObject["dateUtc"]);
                    Assert.AreEqual(jObject["cpu"].ToString(), "90");
                    Assert.AreEqual(jObject["memory"].ToString(), "4200");
                    Assert.AreEqual(jObject["numberOfOpenTcpConnection"].ToString(), "10");
                    JToken threadInfo = jObject["threadInfo"];
                    // Loop until the thread info is available.
                    if (threadInfo["threadWaitIntervalInMs"] == null)
                    {
                        retry = true;
                    }
                    else
                    {
                        Assert.IsNotNull(threadInfo["threadWaitIntervalInMs"].ToString());
                        retry = false;
                    }

                    if (retry)
                    {
                        Thread.Sleep(1);
                    }
                } while (retry);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = currentCulture;
                Thread.CurrentThread.CurrentUICulture = currentUICulture;
            }

        }

        private static void ResetCpuMonitorRefreshInterval()
        {
            CpuMonitor.OverrideRefreshInterval(
                TimeSpan.FromSeconds(CpuMonitor.DefaultRefreshIntervalInSeconds));
        }
    }
}