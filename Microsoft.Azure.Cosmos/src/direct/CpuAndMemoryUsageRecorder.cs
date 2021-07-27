//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Documents.Rntbd.SystemUsageMonitor;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal sealed class CpuAndMemoryUsageRecorder
    {
        private readonly CpuLoad[] cpuLoadBuffer;
        private readonly MemoryLoad[] memoryBuffer;
        private readonly int historyLength;

        internal readonly string identifier;
        internal readonly TimeSpan refreshInterval;

        private int clockHand = 0;
        private int memoryClockHand = 0;

        private TimeSpan nextTimeStamp;

        public CpuLoadHistory CpuUsage { set; get; }
        public MemoryLoadHistory MemoryUsage { set; get; }

        internal CpuAndMemoryUsageRecorder(string identifier, int historyLength, TimeSpan refreshInterval)
        {
            this.identifier = identifier;
            this.historyLength = historyLength;
            this.refreshInterval = refreshInterval;

            this.cpuLoadBuffer = new CpuLoad[this.historyLength];
            this.memoryBuffer = new MemoryLoad[this.historyLength];
        }

        public void RecordUsage(SystemUtilizationReaderBase systemUtilizationReader, Stopwatch watch)
        {
            if (!this.IsEligibleForRecording(watch))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            this.nextTimeStamp = watch.Elapsed.Add(refreshInterval);

            this.RecordMemoryUsage(systemUtilizationReader, now);
            this.RecordCpuUsage(systemUtilizationReader, now);
        }

        private void RecordCpuUsage(SystemUtilizationReaderBase systemUtilizationReader, DateTime now)
        {
            List<CpuLoad> cpuLoadHistory = new List<CpuLoad>(this.cpuLoadBuffer.Length);
            this.cpuLoadBuffer[this.clockHand] = new CpuLoad(now, systemUtilizationReader.GetSystemWideCpuUsage());

            for (int i = 0; i < this.cpuLoadBuffer.Length; i++)
            {
                int index = (this.clockHand + i) % this.cpuLoadBuffer.Length;
                if (this.cpuLoadBuffer[index].Timestamp != DateTime.MinValue)
                {
                    cpuLoadHistory.Add(this.cpuLoadBuffer[index]);
                }

            }

            this.clockHand = (this.clockHand + 1) % this.cpuLoadBuffer.Length;
            this.CpuUsage = new CpuLoadHistory(
                new ReadOnlyCollection<CpuLoad>(cpuLoadHistory),
                this.refreshInterval);
        }

        private void RecordMemoryUsage(SystemUtilizationReaderBase systemUtilizationReader, DateTime now)
        {
            List<MemoryLoad> memoryLoadHistory = new List<MemoryLoad>(this.memoryBuffer.Length);
            this.memoryBuffer[this.memoryClockHand] = new MemoryLoad(now, systemUtilizationReader.GetSystemWideMemoryUsage());
           
            for (int i = 0; i < this.memoryBuffer.Length; i++)
            {
                int index = (this.memoryClockHand + i) % this.memoryBuffer.Length;
                if (this.memoryBuffer[index].Timestamp != DateTime.MinValue)
                {
                    memoryLoadHistory.Add(this.memoryBuffer[index]);
                }

            }

            this.memoryClockHand = (this.memoryClockHand + 1) % this.memoryBuffer.Length;
            this.MemoryUsage = new MemoryLoadHistory(
                new ReadOnlyCollection<MemoryLoad>(memoryLoadHistory),
                this.refreshInterval);
        }

        private bool IsEligibleForRecording(Stopwatch watch) => TimeSpan.Compare(watch.Elapsed, nextTimeStamp) >= 0;
    }
}