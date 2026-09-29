//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Linq;

    internal sealed class CpuLoadHistory
    {
        internal ReadOnlyCollection<CpuLoad> CpuLoad { get; }
        private readonly TimeSpan monitoringInterval;
        private readonly Lazy<bool> cpuOverload;
        private readonly Lazy<string> cpuloadHistory;

        public CpuLoadHistory(ReadOnlyCollection<CpuLoad> cpuLoad, TimeSpan monitoringInterval)
        {
            if (cpuLoad == null)
            {
                throw new ArgumentNullException(nameof(cpuLoad));
            }
            this.CpuLoad = cpuLoad;

            this.cpuloadHistory = new Lazy<string>(() =>
            {
                if (this.CpuLoad?.Count == 0)
                {
                    return "empty";
                }
                return string.Join(", ", this.CpuLoad);
            });

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

        // Currently used only for unit tests.
        internal DateTime LastTimestamp { get { return this.CpuLoad.Last().Timestamp; } }

        public bool IsCpuOverloaded
        {
            get
            {
                return this.cpuOverload.Value;
            }
        }

        public override string ToString()
        {
            return this.cpuloadHistory.Value;
        }

        private bool GetCpuOverload()
        {
            for (int i = 0; i < this.CpuLoad.Count; i++)
            {
                if (this.CpuLoad[i].Value > 90.0)
                {
                    return true;
                }
            }

            // This signal is fragile, because the timestamps come from
            // a non-monotonic clock that might have gotten adjusted by
            // e.g. NTP.
            for (int i = 0; i < this.CpuLoad.Count - 1; i++)
            {
                if (this.CpuLoad[i + 1].Timestamp.Subtract(
                        this.CpuLoad[i].Timestamp).TotalMilliseconds >
                    1.5 * this.monitoringInterval.TotalMilliseconds)
                {
                    return true;
                }

            }

            return false;
        }
    }
}