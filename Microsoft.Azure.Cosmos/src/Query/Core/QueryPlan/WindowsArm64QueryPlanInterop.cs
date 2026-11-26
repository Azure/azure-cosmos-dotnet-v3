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

    internal sealed class WindowsArm64QueryPlanInterop : IQueryPlanInterop
    {
        private readonly Lazy<bool> libraryLoaded;

        private WindowsArm64QueryPlanInterop()
        {
            this.libraryLoaded = new Lazy<bool>(this.TryLoadLibrary);
        }

        public static IQueryPlanInterop Instance { get; } = new WindowsArm64QueryPlanInterop();

        public bool BypassQueryParsing()
        {
            return !this.libraryLoaded.Value;
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
            return NativeMethods.GetPartitionKeyRangesFromQuery4(
                serviceProvider,
                query,
                options,
                pathTokens,
                pathTokenLengths,
                pathCount,
                vectorPolicy,
                (uint)(vectorPolicy?.Length ?? 0),
                buffer,
                bufferLength,
                out resultLength);
        }

        private bool TryLoadLibrary()
        {
            TryCatch<IntPtr> tryCreateServiceProvider = QueryPartitionProvider.TryCreateServiceProvider(this, "{}");
            if (tryCreateServiceProvider.Succeeded)
            {
                Marshal.Release(tryCreateServiceProvider.Result);
                return true;
            }

            DefaultTrace.TraceWarning(
                "QueryPlanInterop native library for Windows {0} could not be loaded: {1}. Query plans will be retrieved from the Gateway.",
                RuntimeInformation.ProcessArchitecture,
                tryCreateServiceProvider.Exception.Message);
            return false;
        }

        private static class NativeMethods
        {
            private const string Library = "Cosmos.QueryPlanInterop.dll";

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
                [MarshalAs(UnmanagedType.LPWStr)] string query,
                PartitionKeyRangesOptions options,
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pathTokens,
                [In] uint[] pathTokenLengths,
                uint pathCount,
                [MarshalAs(UnmanagedType.LPWStr)] string vectorPolicy,
                uint vectorPolicyLength,
                IntPtr buffer,
                uint bufferLength,
                out uint resultLength);
        }
    }
}