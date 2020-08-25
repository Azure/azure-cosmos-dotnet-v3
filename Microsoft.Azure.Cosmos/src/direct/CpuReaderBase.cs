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

    internal abstract class CpuReaderBase
    {
        private static readonly Lazy<CpuReaderBase> singletonInstance = new Lazy<CpuReaderBase>(
            CpuReaderBase.Create,
            LazyThreadSafetyMode.ExecutionAndPublication);

        // Just for testing purposes - no need to be thread-safe
        private static CpuReaderBase singletonOverride;

        protected CpuReaderBase()
        {
        }

        public static CpuReaderBase SingletonInstance
        {
            get
            {
                CpuReaderBase snapshot;
                if ((snapshot = singletonOverride) != null)
                {
                    return snapshot;
                }

                return singletonInstance.Value;
            }
        }

        internal static void ApplySingletonOverride(CpuReaderBase readerOverride)
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

#if NETFX
        private static CpuReaderBase Create()
        {
            return new WindowsCpuReader();
        }
#else
        private static CpuReaderBase Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsCpuReader();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxCpuReader();
            }
            else
            {
                return new UnsupportedCpuReader();
            }
        }
#endif // NETFX

        protected abstract float GetSystemWideCpuUsageCore();
    }
}