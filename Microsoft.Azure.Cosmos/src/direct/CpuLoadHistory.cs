//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Threading;

    internal sealed class CpuLoadHistory
    {
        private readonly ReadOnlyCollection<CpuLoad> cpuLoad;
        private readonly TimeSpan monitoringInterval;
        private readonly Lazy<bool> cpuOverload;

        public CpuLoadHistory(ReadOnlyCollection<CpuLoad> cpuLoad, TimeSpan monitoringInterval)
        {
            if (cpuLoad == null)
            {
                throw new ArgumentNullException(nameof(cpuLoad));
            }
            this.cpuLoad = cpuLoad;

            if (monitoringInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monitoringInterval),
                    monitoringInterval,
                    string.Format("{0} must be strictly positive", nameof(monitoringInterval)));
            }
            this.monitoringInterval = monitoringInterval;
            this.cpuOverload = new Lazy<bool>(
                new Func<bool>(this.GetCpuOverload),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public bool IsCpuOverloaded
        {
            get
            {
                return this.cpuOverload.Value;
            }
        }

        // Currently used only for unit tests.
        internal DateTime LastTimestamp { get { return this.cpuLoad[this.cpuLoad.Count - 1].Timestamp; } }

        public override string ToString()
        {
            if (this.cpuLoad?.Count == 0)
            {
                return "empty";
            }
            return string.Join(", ", this.cpuLoad);
        }

        private bool GetCpuOverload()
        {
            for (int i = 0; i < this.cpuLoad.Count; i++)
            {
                if (this.cpuLoad[i].Value > 90.0)
                {
                    return true;
                }
            }

            // This signal is fragile, because the timestamps come from
            // a non-monotonic clock that might have gotten adjusted by
            // e.g. NTP.
            for (int i = 0; i < this.cpuLoad.Count - 1; i++)
            {
                if (this.cpuLoad[i + 1].Timestamp.Subtract(
                        this.cpuLoad[i].Timestamp).TotalMilliseconds >
                    1.5 * this.monitoringInterval.TotalMilliseconds)
                {
                    return true;
                }
            }
            return false;
        }
    }
}