//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using ServiceInteropWrapper = Microsoft.Azure.Documents.ServiceInteropWrapper;

    internal static class QueryPlanInterop
    {
        private const uint UnsupportedPlatformHResult = 0x80070032;

        private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static readonly bool IsSupportedLinuxProcess = IsLinux
            && IsSupportedLinuxPlatform(OSPlatform.Linux, RuntimeInformation.ProcessArchitecture);

        private static readonly Lazy<bool> LinuxLibraryAvailable = new Lazy<bool>(() =>
        {
            TryCatch<IntPtr> result = QueryPartitionProvider.TryCreateServiceProvider("{}");
            if (result.Failed)
            {
                DefaultTrace.TraceWarning("Linux QueryPlanInterop is unavailable. Falling back to gateway query planning.");
                return false;
            }

            ReleaseServiceProvider(result.Result);
            return true;
        });

        private static readonly Encoding WideStringEncoding = new UTF32Encoding(
            bigEndian: false,
            byteOrderMark: false,
            throwOnInvalidCharacters: true);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ReleaseDelegate(IntPtr serviceProvider);

        internal static bool SupportsLinuxPlanning()
        {
            return IsSupportedLinuxProcess && LinuxLibraryAvailable.Value;
        }

        internal static bool IsSupportedLinuxPlatform(
            OSPlatform platform,
            Architecture architecture)
        {
            return platform == OSPlatform.Linux
                && (architecture == Architecture.X64 || architecture == Architecture.Arm64);
        }

        internal static uint CreateServiceProvider(
            string configuration,
            out IntPtr serviceProvider)
        {
            if (IsLinux)
            {
                if (!IsSupportedLinuxProcess)
                {
                    serviceProvider = IntPtr.Zero;
                    return UnsupportedPlatformHResult;
                }

                byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? Arm64.CreateServiceProvider(
                        utf8Configuration,
                        out serviceProvider)
                    : X64.CreateServiceProvider(
                        utf8Configuration,
                        out serviceProvider);
            }

            return ServiceInteropWrapper.CreateServiceProvider(
                configuration,
                out serviceProvider);
        }

        internal static uint UpdateServiceProvider(
            IntPtr serviceProvider,
            string configuration)
        {
            if (IsLinux)
            {
                byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? Arm64.UpdateServiceProvider(
                        serviceProvider,
                        utf8Configuration)
                    : X64.UpdateServiceProvider(
                        serviceProvider,
                        utf8Configuration);
            }

            return ServiceInteropWrapper.UpdateServiceProvider(
                serviceProvider,
                configuration);
        }

        internal static void ReleaseServiceProvider(IntPtr serviceProvider)
        {
            if (IsLinux)
            {
                IntPtr vtable = Marshal.ReadIntPtr(serviceProvider);
                IntPtr release = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size);
                Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(release)(serviceProvider);
            }
            else
            {
                Marshal.Release(serviceProvider);
            }
        }

        internal static uint GetPartitionKeyRangesFromQuery4(
            IntPtr serviceProvider,
            string query,
            ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
            string[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            string vectorPolicy,
            int vectorPolicyLength,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength)
        {
            if (IsLinux)
            {
                return GetPartitionKeyRangesFromQueryOnLinux(
                    serviceProvider,
                    query,
                    options,
                    pathTokens,
                    pathTokenLengths,
                    pathCount,
                    vectorPolicy,
                    buffer,
                    bufferLength,
                    out resultLength);
            }

            return ServiceInteropWrapper.GetPartitionKeyRangesFromQuery4(
                serviceProvider,
                query,
                options,
                pathTokens,
                pathTokenLengths,
                pathCount,
                vectorPolicy,
                vectorPolicyLength,
                buffer,
                bufferLength,
                out resultLength);
        }

        internal static byte[] EncodeWideString(string value)
        {
            return value == null ? null : WideStringEncoding.GetBytes(value + '\0');
        }

        private static uint GetPartitionKeyRangesFromQueryOnLinux(
            IntPtr serviceProvider,
            string query,
            ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
            string[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            string vectorPolicy,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength)
        {
            // Linux wchar_t is UTF-32, not the UTF-16 used by LPWStr on Windows.
            byte[] wideQuery = EncodeWideString(query);
            byte[] wideVectorPolicy = EncodeWideString(vectorPolicy);
            uint wideVectorPolicyLength = wideVectorPolicy == null ? 0 : (uint)((wideVectorPolicy.Length / 4) - 1);
            IntPtr[] widePathTokens = new IntPtr[pathTokens.Length];
            try
            {
                for (int i = 0; i < pathTokens.Length; i++)
                {
                    byte[] token = EncodeWideString(pathTokens[i]);
                    widePathTokens[i] = Marshal.AllocHGlobal(token.Length);
                    Marshal.Copy(token, 0, widePathTokens[i], token.Length);
                }

                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? Arm64.GetPartitionKeyRangesFromQuery4(
                        serviceProvider,
                        wideQuery,
                        options,
                        widePathTokens,
                        pathTokenLengths,
                        pathCount,
                        wideVectorPolicy,
                        wideVectorPolicyLength,
                        buffer,
                        bufferLength,
                        out resultLength)
                    : X64.GetPartitionKeyRangesFromQuery4(
                        serviceProvider,
                        wideQuery,
                        options,
                        widePathTokens,
                        pathTokenLengths,
                        pathCount,
                        wideVectorPolicy,
                        wideVectorPolicyLength,
                        buffer,
                        bufferLength,
                        out resultLength);
            }
            finally
            {
                foreach (IntPtr token in widePathTokens)
                {
                    Marshal.FreeHGlobal(token);
                }
            }
        }

        private static class X64
        {
            private const string Library = "libQueryPlanInterop.amd64.so";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint CreateServiceProvider(
                [In] byte[] configuration,
                out IntPtr serviceProvider);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint UpdateServiceProvider(
                IntPtr serviceProvider,
                [In] byte[] configuration);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint GetPartitionKeyRangesFromQuery4(
                IntPtr serviceProvider,
                [In] byte[] query,
                ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
                [In] IntPtr[] pathTokens,
                [In] uint[] pathTokenLengths,
                uint pathCount,
                [In] byte[] vectorPolicy,
                uint vectorPolicyLength,
                IntPtr buffer,
                uint bufferLength,
                out uint resultLength);
        }

        private static class Arm64
        {
            private const string Library = "libQueryPlanInterop.arm64.so";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint CreateServiceProvider(
                [In] byte[] configuration,
                out IntPtr serviceProvider);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint UpdateServiceProvider(
                IntPtr serviceProvider,
                [In] byte[] configuration);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern uint GetPartitionKeyRangesFromQuery4(
                IntPtr serviceProvider,
                [In] byte[] query,
                ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
                [In] IntPtr[] pathTokens,
                [In] uint[] pathTokenLengths,
                uint pathCount,
                [In] byte[] vectorPolicy,
                uint vectorPolicyLength,
                IntPtr buffer,
                uint bufferLength,
                out uint resultLength);
        }
    }
}