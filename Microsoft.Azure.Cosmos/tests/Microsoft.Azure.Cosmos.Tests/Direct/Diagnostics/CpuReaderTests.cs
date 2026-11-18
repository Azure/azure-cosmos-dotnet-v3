//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="CpuReaderBase"/>  scenarios.
    /// </summary>
    [TestClass]
    public class CpuReaderTests
    {
        [TestMethod]
        [Owner("fabianm")]
        public async Task UnsupportedCpuReaderTests()
        {
            UnsupportedSystemUtilizationReader reader = new UnsupportedSystemUtilizationReader();

            float expectedFirstValue = Single.NaN;
            float expectedSubsequentValues = Single.NaN;

            Assert.AreEqual(expectedFirstValue, reader.GetSystemWideCpuUsage());
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            Assert.AreEqual(expectedSubsequentValues, reader.GetSystemWideCpuUsage());
        }

        [TestMethod]
        [Timeout(30000)]
        [Owner("fabianm")]
        public void WindowsCpuReaderTests()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("This test requires the Windows system-utilization APIs.");
            }

            WindowsSystemUtilizationReader reader = new WindowsSystemUtilizationReader();
            float firstValue = reader.GetSystemWideCpuUsage();
            long? firstMemoryValue = reader.GetSystemWideMemoryAvailabilty();

            Assert.AreEqual(false, Single.IsNaN(firstValue));
            Assert.IsTrue(firstValue >= 0);
            Assert.IsNotNull(firstMemoryValue);
            Assert.IsTrue(firstMemoryValue >= 0);

            Stopwatch watch = Stopwatch.StartNew();
            float subsequentValue;
            // burning some CPU
            while (watch.ElapsedMilliseconds < 50 || Single.IsNaN(subsequentValue = reader.GetSystemWideCpuUsage()))
            {}
            watch.Stop();

            long? subsequentMemoryValue = reader.GetSystemWideMemoryAvailabilty();
            Assert.AreEqual(false, Single.IsNaN(subsequentValue));
            Assert.IsTrue(subsequentValue > 0);
            Assert.IsNotNull(subsequentMemoryValue);
            Assert.IsTrue(subsequentMemoryValue > 0);
        }

        [TestMethod]
        [Owner("fabianm")]
        public void LinuxSystemUsageReaderTests()
        {
            ProcFsSimulator.LinuxVersion[] versions = new[]
            {
                ProcFsSimulator.LinuxVersion.BeforeLinux2_5_41,
                ProcFsSimulator.LinuxVersion.SinceLinux2_5_41,
                ProcFsSimulator.LinuxVersion.SinceLinux2_6_0_test4,
                ProcFsSimulator.LinuxVersion.SinceLinux2_6_11,
                ProcFsSimulator.LinuxVersion.SinceLinux2_6_24,
                ProcFsSimulator.LinuxVersion.SinceLinux2_6_33,
                ProcFsSimulator.LinuxVersion.Future,
            };

            foreach (ProcFsSimulator.LinuxVersion version in versions)
            {
                Console.WriteLine("TEST CASE: {0}", version);
                // Valid meminfo file with Memeory Available information
                using (ProcFsSimulator simulator = new ProcFsSimulator(GetFixturePath("meminfo.txt")))
                {
                    // First call on Linux will return 0 because time elapsed is unknown
                    simulator.SetFirstProcFsLine(simulator.GenerateValidCpuUsageLine(version));
                    float firstValue = simulator.Reader.GetSystemWideCpuUsage();
                    Assert.AreEqual(false, Single.IsNaN(firstValue));
                    Assert.AreEqual(0, firstValue);

                    simulator.SetFirstProcFsLine(simulator.GenerateValidCpuUsageLine(version));
                    float subsequentValue = simulator.Reader.GetSystemWideCpuUsage();
                    long? subsequentMemoryValue = simulator.Reader.GetSystemWideMemoryAvailabilty();
                    Assert.AreEqual(false, Single.IsNaN(subsequentValue));
                    Assert.IsTrue(subsequentValue > 0);
                    Assert.IsNotNull(subsequentMemoryValue);
                    Assert.AreEqual(1535676, subsequentMemoryValue);
                }

                // Valid meminfo file without Memory Available information
                using (ProcFsSimulator simulator = new ProcFsSimulator(GetFixturePath("meminfoWithoutMemAvl.txt")))
                {
                    simulator.SetFirstProcFsLine(simulator.GenerateValidCpuUsageLine(version));
                    float firstValue = simulator.Reader.GetSystemWideCpuUsage();
                    long? subsequentMemoryValue = simulator.Reader.GetSystemWideMemoryAvailabilty();
                    Assert.AreEqual(false, Single.IsNaN(firstValue));
                    Assert.AreEqual(0, firstValue);
                    Assert.IsNotNull(subsequentMemoryValue);
                    Assert.AreEqual(1376380, subsequentMemoryValue);
                }

                // InValid meminfo file and invalid/unexpected format in the proc fs file
                using (ProcFsSimulator simulator = new ProcFsSimulator(GetFixturePath("meminfoInvalid.txt")))
                {
                    simulator.SetFirstProcFsLine(Guid.NewGuid().ToString("N"));
                    float valueForUnexpectedSyntax = simulator.Reader.GetSystemWideCpuUsage();
                    long? subsequentMemoryValue = simulator.Reader.GetSystemWideMemoryAvailabilty();
                    Assert.AreEqual(true, Single.IsNaN(valueForUnexpectedSyntax));
                    Assert.IsNull(subsequentMemoryValue);
                }
            }
        }

        private static string GetFixturePath(string name) =>
            Path.Combine(AppContext.BaseDirectory, "Direct", "Diagnostics", "TestFiles", name);

        private sealed class ProcFsSimulator : IDisposable
        {
            private static readonly Random rnd = new Random(42);

            private readonly string cpuUsageFilePath; 
            private readonly string memoryInfoFilePath;
            private readonly LinuxSystemUtilizationReader cpuReader;
            private readonly int[] usageValues;

            public ProcFsSimulator() : this(Path.GetTempFileName(), Path.GetTempFileName())
            {

            }

            public ProcFsSimulator(string memoryInfoFilePath) : this(Path.GetTempFileName(), memoryInfoFilePath)
            {

            }

            public ProcFsSimulator(string cpuInfoFilePath, string memoryInfoFilePath)
            {
                this.cpuUsageFilePath = cpuInfoFilePath;
                this.memoryInfoFilePath = memoryInfoFilePath;

                this.cpuReader = new LinuxSystemUtilizationReader(this.cpuUsageFilePath, this.memoryInfoFilePath);

                this.usageValues = new int[(int)LinuxVersion.Future];
            }

            public SystemUtilizationReaderBase Reader => this.cpuReader;

            public string GenerateValidCpuUsageLine(LinuxVersion version)
            {
                for (int i = 0; i < this.usageValues.Length; i++)
                {
                    this.usageValues[i] += rnd.Next(1, 10000);
                }

                return "cpu  " +
                    String.Join(
                        " ",
                        new ArraySegment<int>(this.usageValues, 0, (int)version));
            }

            public void SetFirstProcFsLine(string firstLine)
            {
                Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                using (FileStream fs = new FileStream(this.cpuUsageFilePath, FileMode.Open, FileAccess.Write, FileShare.Write))
                using (StreamWriter writer = new StreamWriter(fs, utf8, bufferSize: 1024, leaveOpen: false))
                {
                    writer.WriteLine(firstLine);
                    writer.WriteLine(Guid.NewGuid().ToString("N"));

                    writer.Flush();
                    fs.Flush();
                }
            }

            public void Dispose()
            {
                if (!String.IsNullOrWhiteSpace(this.cpuUsageFilePath))
                {
                    File.Delete(this.cpuUsageFilePath);
                }
            }

            public enum LinuxVersion
            {
                BeforeLinux2_5_41 = 4,
                SinceLinux2_5_41 = 5,
                SinceLinux2_6_0_test4 = 7,
                SinceLinux2_6_11 = 8,
                SinceLinux2_6_24 = 9,
                SinceLinux2_6_33 = 10,
                Future = 12
            }
        }
    }
}