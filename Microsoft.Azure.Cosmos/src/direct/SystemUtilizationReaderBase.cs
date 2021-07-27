//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal abstract class SystemUtilizationReaderBase
    {
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

        public long GetSystemWideMemoryUsage()
        {
            try
            {
                return this.GetSystemWideMemoryUsageCore();
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceError(
                    "Reading the system-wide Memory usage failed. Exception: {0}",
                    exception);

                return -1;
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

        protected abstract long GetSystemWideMemoryUsageCore();
    }
}