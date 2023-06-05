//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal abstract class SystemUtilizationReaderBase
    {
        private float cachedCpuUtilization = Single.NaN;
        private long lastCpuUsageReadTimeTicks = DateTime.MinValue.Ticks;
        private static readonly Lazy<SystemUtilizationReaderBase> singletonInstance = new Lazy<SystemUtilizationReaderBase>(
            SystemUtilizationReaderBase.Create,
            LazyThreadSafetyMode.ExecutionAndPublication);

        // Just for testing purposes - no need to be thread-safe
        private static SystemUtilizationReaderBase singletonOverride;

        protected SystemUtilizationReaderBase()
        {
        }

        public static SystemUtilizationReaderBase SingletonInstance
        {
            get
            {
                SystemUtilizationReaderBase snapshot;
                if ((snapshot = singletonOverride) != null)
                {
                    return snapshot;
                }

                return singletonInstance.Value;
            }
        }

        internal static void ApplySingletonOverride(SystemUtilizationReaderBase readerOverride)
        {
            singletonOverride = readerOverride;
        }

        /// <summary>
        /// Caches the CPU utilization for an user defined eviction time period and evicts the cache atomically based on
        /// the eviction time period.
        /// </summary>
        /// <param name="cacheEvictionTimeInSeconds">A <see cref="TimeSpan"/> containing the cache eviction time in seconds.</param>
        /// <returns>A float value containing the cached cpu usage.</returns>
        public float GetSystemWideCpuUsageCached(
            TimeSpan cacheEvictionTimeInSeconds)
        {
            long snapshotTimestampTicks = Volatile.Read(ref this.lastCpuUsageReadTimeTicks);
            long currentTimestampTicks = Stopwatch.GetTimestamp();
            long delta = currentTimestampTicks - snapshotTimestampTicks;

            if (delta >= cacheEvictionTimeInSeconds.Ticks)
            {
                long updatedLastCpuUsageReadTimeTicks = Interlocked.CompareExchange(
                    location1: ref this.lastCpuUsageReadTimeTicks,
                    value: currentTimestampTicks,
                    comparand: snapshotTimestampTicks);

                // This means that the value was not updated by any other thread and the compare exchange operation was atomically successful.
                if (updatedLastCpuUsageReadTimeTicks == snapshotTimestampTicks)
                {
                    Volatile.Write(ref this.cachedCpuUtilization, this.GetSystemWideCpuUsage());
                }
            }

            return Volatile.Read(ref this.cachedCpuUtilization);
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Intentional catch-all-rethrow here t log exception")]
        public float GetSystemWideCpuUsage()
        {
            try
            {
                return this.GetSystemWideCpuUsageCore();
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceError(
                    "Reading the system-wide CPU usage failed. Exception: {0}",
                    exception);

                return Single.NaN;
            }
        }

        public long? GetSystemWideMemoryAvailabilty()
        {
            try
            {
                return this.GetSystemWideMemoryAvailabiltyCore();
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceError(
                    "Reading the system-wide Memory availability failed. Exception: {0}",
                    exception);

                return null;
            }
        }

#if NETFX
        private static SystemUtilizationReaderBase Create()
        {
            return new WindowsSystemUtilizationReader();
        }
#else
        private static SystemUtilizationReaderBase Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsSystemUtilizationReader();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxSystemUtilizationReader();
            } else
            {
                return new UnsupportedSystemUtilizationReader();
            }
        }
#endif // NETFX

        protected abstract float GetSystemWideCpuUsageCore();

        protected abstract long? GetSystemWideMemoryAvailabiltyCore();
    }
}