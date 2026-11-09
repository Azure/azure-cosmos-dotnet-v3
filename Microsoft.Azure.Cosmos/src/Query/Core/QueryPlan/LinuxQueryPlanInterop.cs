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

    internal sealed class LinuxQueryPlanInterop : IQueryPlanInterop
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

        public static IQueryPlanInterop Instance { get; } = new LinuxQueryPlanInterop();

        public bool BypassQueryParsing()
        {
            return !ConfigurationManager.IsQueryInteropEnabled() || !this.libraryLoaded.Value;
        }

        public uint CreateServiceProvider(string configuration, out IntPtr serviceProvider)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return NativeMethods.CreateServiceProvider(utf8Configuration, out serviceProvider);
        }

        public uint UpdateServiceProvider(IntPtr serviceProvider, string configuration)
        {
            byte[] utf8Configuration = Encoding.UTF8.GetBytes(configuration + '\0');
            return NativeMethods.UpdateServiceProvider(serviceProvider, utf8Configuration);
        }

        public uint GetPartitionKeyRanges(
            IntPtr serviceProvider,
            string query,
            PartitionKeyRangesOptions options,
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

                return NativeMethods.GetPartitionKeyRangesFromQuery4(
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
            Exception exception;
            if (tryCreateServiceProvider.Failed)
            {
                exception = tryCreateServiceProvider.Exception;
            }
            else
            {
                try
                {
                    Marshal.Release(tryCreateServiceProvider.Result);
                    return true;
                }
                catch (PlatformNotSupportedException ex)
                {
                    // Marshal.Release supports Linux only on .NET 6 and later.
                    exception = ex;
                }
            }

            DefaultTrace.TraceWarning(
                "QueryPlanInterop native library for Linux {0} could not be loaded: {1}. Query plans will be retrieved from the Gateway.",
                RuntimeInformation.ProcessArchitecture,
                exception.Message);
            return false;
        }

        private static class NativeMethods
        {
            // The package ships this file name for every Linux architecture under runtimes/<rid>/native.
            private const string Library = "libQueryPlanInterop.so";

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
                PartitionKeyRangesOptions options,
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
