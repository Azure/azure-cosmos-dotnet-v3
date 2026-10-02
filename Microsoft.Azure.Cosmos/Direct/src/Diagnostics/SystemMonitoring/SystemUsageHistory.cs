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
    using System.Text;

    internal class SystemUsageHistory
    {
        internal ReadOnlyCollection<SystemUsageLoad> Values { get; }

        private readonly TimeSpan monitoringInterval;
        private readonly Lazy<string> loadHistory;
        private readonly Lazy<bool?> cpuHigh;
        private readonly Lazy<bool?> cpuThreadStarvation;

        public SystemUsageHistory(ReadOnlyCollection<SystemUsageLoad> data, 
            TimeSpan monitoringInterval)
        {
            this.Values = data ?? throw new ArgumentNullException(nameof(data));
            if(this.Values.Count > 0)
            {
                this.LastTimestamp = this.Values.Last().Timestamp;
            }
            else
            {
                this.LastTimestamp = DateTime.MinValue;
            }

            this.loadHistory = new Lazy<string>(() => {
                if(this.Values == null || this.Values.Count == 0)
                {
                    return "{\"systemHistory\":\"Empty\"}";
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("{\"systemHistory\":[");
                foreach (SystemUsageLoad systemUsage in this.Values)
                {
                    systemUsage.AppendJsonString(stringBuilder);
                    stringBuilder.Append(",");
                }

                stringBuilder.Length--; // Remove the extra comma at the end
                stringBuilder.Append("]}");

                return stringBuilder.ToString();
            });

            if (monitoringInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monitoringInterval),
                    monitoringInterval,
                    string.Format("{0} must be strictly positive", nameof(monitoringInterval)));
            }

            this.monitoringInterval = monitoringInterval;
            this.cpuHigh = new Lazy<bool?>(this.GetCpuHigh,
                LazyThreadSafetyMode.ExecutionAndPublication);
            this.cpuThreadStarvation = new Lazy<bool?>(this.GetCpuThreadStarvation,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal DateTime LastTimestamp { get; }

        public override string ToString()
        {
            return this.loadHistory.Value;
        }

        /// <summary>
        /// Use the cached string since the system load will be the same
        /// for multiple requests.
        /// </summary>
        public void AppendJsonString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(this.ToString());
        }

        public bool? IsCpuHigh
        {
            get
            {
                return this.cpuHigh.Value;
            }
        }

        public bool? IsCpuThreadStarvation
        {
            get
            {
                return this.cpuThreadStarvation.Value;
            }
        }

        private bool? GetCpuHigh()
        {
            if(this.Values.Count == 0)
            {
                return null;
            }

            // If the CPU value is not set return null.
            bool? isCpuHigh = null;
            foreach(SystemUsageLoad systemUsageLoad in this.Values)
            {
                if (!systemUsageLoad.CpuUsage.HasValue)
                {
                    continue;
                }

                if(systemUsageLoad.CpuUsage.Value > 90.0)
                {
                    return true;
                }
                else
                {
                    isCpuHigh = false;
                }
            }

            return isCpuHigh;
        }

        private bool? GetCpuThreadStarvation()
        {
            if (this.Values.Count == 0)
            {
                return null;
            }

            // If the CPU value is not set return null.
            bool? isThreadStarvation = null;
            foreach (SystemUsageLoad systemUsageLoad in this.Values)
            {
                if (systemUsageLoad.ThreadInfo == null
                    || !systemUsageLoad.ThreadInfo.IsThreadStarving.HasValue)
                {
                    continue;
                }

                if (systemUsageLoad.ThreadInfo.IsThreadStarving.Value)
                {
                    return true;
                }
                else
                {
                    isThreadStarvation = false;
                }
            }

            return isThreadStarvation;
        }
    }
}