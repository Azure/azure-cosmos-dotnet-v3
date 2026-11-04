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

    internal abstract class LinuxQueryPlanInterop : IQueryPlanInterop
    {
        private static readonly Encoding WideStringEncoding = new UTF32Encoding(
            bigEndian: false,
            byteOrderMark: false,
            throwOnInvalidCharacters: true);

        private readonly Lazy<bool> libraryLoaded;

        private LinuxQueryPlanInterop()
        {
            this.libraryLoaded = new Lazy<bool>(this.TryLoadLibrary);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ReleaseDelegate(IntPtr serviceProvider);

        public bool BypassQueryParsing()
        {
            return !ConfigurationManager.IsQueryInteropEnabled() || !this.libraryLoaded.Value;
        }

        public uint CreateServiceProvider(string configuration, out IntPtr serviceProvider)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return this.CreateServiceProviderNative(utf8Configuration, out serviceProvider);
        }

        public uint UpdateServiceProvider(IntPtr serviceProvider, string configuration)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return this.UpdateServiceProviderNative(serviceProvider, utf8Configuration);
        }

        public void ReleaseServiceProvider(IntPtr serviceProvider)
        {
            IntPtr vtable = Marshal.ReadIntPtr(serviceProvider);
            IntPtr release = Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size);
            Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(release)(serviceProvider);
        }

        public uint GetPartitionKeyRangesFromQuery4(
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

                return this.GetPartitionKeyRangesNative(
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

        protected abstract uint CreateServiceProviderNative(byte[] configuration, out IntPtr serviceProvider);

        protected abstract uint UpdateServiceProviderNative(IntPtr serviceProvider, byte[] configuration);

        protected abstract uint GetPartitionKeyRangesNative(
            IntPtr serviceProvider,
            byte[] query,
            ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
            IntPtr[] pathTokens,
            uint[] pathTokenLengths,
            uint pathCount,
            byte[] vectorPolicy,
            uint vectorPolicyLength,
            IntPtr buffer,
            uint bufferLength,
            out uint resultLength);

        private static byte[] EncodeWideString(string value)
        {
            return value == null ? null : WideStringEncoding.GetBytes(value + '\0');
        }

        private bool TryLoadLibrary()
        {
            TryCatch<IntPtr> tryCreateServiceProvider = QueryPartitionProvider.TryCreateServiceProvider(this, "{}");
            if (tryCreateServiceProvider.Failed)
            {
                DefaultTrace.TraceWarning(
                    "QueryPlanInterop native library for Linux {0} could not be loaded: {1}. Query plans will be retrieved from the Gateway.",
                    RuntimeInformation.ProcessArchitecture,
                    tryCreateServiceProvider.Exception.Message);
                return false;
            }

            this.ReleaseServiceProvider(tryCreateServiceProvider.Result);
            return true;
        }

        internal sealed class X64 : LinuxQueryPlanInterop
        {
            private X64()
            {
            }

            public static IQueryPlanInterop Instance { get; } = new X64();

            protected override uint CreateServiceProviderNative(byte[] configuration, out IntPtr serviceProvider)
            {
                return NativeMethods.CreateServiceProvider(configuration, out serviceProvider);
            }

            protected override uint UpdateServiceProviderNative(IntPtr serviceProvider, byte[] configuration)
            {
                return NativeMethods.UpdateServiceProvider(serviceProvider, configuration);
            }

            protected override uint GetPartitionKeyRangesNative(
                IntPtr serviceProvider,
                byte[] query,
                ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
                IntPtr[] pathTokens,
                uint[] pathTokenLengths,
                uint pathCount,
                byte[] vectorPolicy,
                uint vectorPolicyLength,
                IntPtr buffer,
                uint bufferLength,
                out uint resultLength)
            {
                return NativeMethods.GetPartitionKeyRangesFromQuery4(
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

            private static class NativeMethods
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
        }

        internal sealed class Arm64 : LinuxQueryPlanInterop
        {
            private Arm64()
            {
            }

            public static IQueryPlanInterop Instance { get; } = new Arm64();

            protected override uint CreateServiceProviderNative(byte[] configuration, out IntPtr serviceProvider)
            {
                return NativeMethods.CreateServiceProvider(configuration, out serviceProvider);
            }

            protected override uint UpdateServiceProviderNative(IntPtr serviceProvider, byte[] configuration)
            {
                return NativeMethods.UpdateServiceProvider(serviceProvider, configuration);
            }

            protected override uint GetPartitionKeyRangesNative(
                IntPtr serviceProvider,
                byte[] query,
                ServiceInteropWrapper.PartitionKeyRangesApiOptions options,
                IntPtr[] pathTokens,
                uint[] pathTokenLengths,
                uint pathCount,
                byte[] vectorPolicy,
                uint vectorPolicyLength,
                IntPtr buffer,
                uint bufferLength,
                out uint resultLength)
            {
                return NativeMethods.GetPartitionKeyRangesFromQuery4(
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

            private static class NativeMethods
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
}
