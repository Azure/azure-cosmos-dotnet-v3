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

    internal sealed class LinuxQueryPlanInterop : IQueryPlanInterop
    {
        private static readonly Encoding WideStringEncoding = new UTF32Encoding(
            bigEndian: false,
            byteOrderMark: false,
            throwOnInvalidCharacters: true);

        private readonly CreateServiceProviderDelegate createServiceProvider;
        private readonly UpdateServiceProviderDelegate updateServiceProvider;
        private readonly GetPartitionKeyRangesDelegate getPartitionKeyRanges;
        private readonly Lazy<bool> libraryLoaded;

        private LinuxQueryPlanInterop()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                this.createServiceProvider = Arm64.CreateServiceProvider;
                this.updateServiceProvider = Arm64.UpdateServiceProvider;
                this.getPartitionKeyRanges = Arm64.GetPartitionKeyRangesFromQuery4;
            }
            else
            {
                this.createServiceProvider = X64.CreateServiceProvider;
                this.updateServiceProvider = X64.UpdateServiceProvider;
                this.getPartitionKeyRanges = X64.GetPartitionKeyRangesFromQuery4;
            }

            this.libraryLoaded = new Lazy<bool>(this.TryLoadLibrary);
        }

        private delegate uint CreateServiceProviderDelegate(byte[] configuration, out IntPtr serviceProvider);

        private delegate uint UpdateServiceProviderDelegate(IntPtr serviceProvider, byte[] configuration);

        private delegate uint GetPartitionKeyRangesDelegate(
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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ReleaseDelegate(IntPtr serviceProvider);

        public static IQueryPlanInterop Instance { get; } = new LinuxQueryPlanInterop();

        public bool BypassQueryParsing()
        {
            return !ConfigurationManager.IsQueryInteropEnabled() || !this.libraryLoaded.Value;
        }

        public uint CreateServiceProvider(string configuration, out IntPtr serviceProvider)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return this.createServiceProvider(utf8Configuration, out serviceProvider);
        }

        public uint UpdateServiceProvider(IntPtr serviceProvider, string configuration)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return this.updateServiceProvider(serviceProvider, utf8Configuration);
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

                return this.getPartitionKeyRanges(
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
