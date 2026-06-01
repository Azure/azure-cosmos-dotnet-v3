// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeAsyncPoc
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Raw P/Invoke surface for cosmos_async_poc.dll. Mirrors the C ABI
    /// declared in <c>cosmos_async_poc.h</c> exactly — every signature change
    /// must be applied here and to the Rust crate in lockstep.
    /// </summary>
    /// <remarks>
    /// All handles are opaque <see cref="IntPtr"/>. Lifetimes:
    ///   * Runtime — one per process; free last.
    ///   * Driver  — many per runtime; free before runtime.
    ///   * CQ      — many per driver-set; shut down + free after every op
    ///                released.
    ///   * Op      — released by host independently of completion delivery
    ///                (invariant I5).
    ///   * Response — owned by host once <see cref="cosmos_cq_wait"/>
    ///                hands it back; free via <see cref="cosmos_response_free"/>.
    /// </remarks>
    internal static class NativeMethods
    {
        public const string LibraryName = "cosmos_async_poc";

        /// <summary>Status codes returned by every fallible C entry point.</summary>
        public enum CosmosStatus : int
        {
            Ok = 0,
            InvalidArg = 1,
            Cancelled = 2,
            QueueShutdown = 3,
            ServiceError = 4,
            InternalError = 5,
        }

        // ---- runtime -----------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_runtime_new(uint workerThreads);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_runtime_free(IntPtr runtime);

        // ---- driver ------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosStatus cosmos_driver_new(
            IntPtr runtime,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string endpoint,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string masterKey,
            out IntPtr outDriver);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_driver_free(IntPtr driver);

        // ---- completion queue --------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_cq_new(uint capacity);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_cq_shutdown(IntPtr cq);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_cq_free(IntPtr cq);

        /// <summary>
        /// Blocks the calling thread until one completion is available or
        /// <paramref name="timeoutMs"/> elapses. <c>timeout_ms = 0</c> means
        /// block forever (but the wait loop polls shutdown every 200 ms so
        /// cancel still works).
        /// </summary>
        /// <returns>
        /// Wait-outcome (Ok / Cancelled / QueueShutdown / InvalidArg) —
        /// the operation outcome is in <paramref name="outStatus"/>.
        /// </returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosStatus cosmos_cq_wait(
            IntPtr cq,
            ulong timeoutMs,
            out UIntPtr outUserData,
            out CosmosStatus outStatus,
            out IntPtr outResponse);

        // ---- operation ---------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosStatus cosmos_read_item(
            IntPtr driver,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string databaseId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string containerId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string partitionKey,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr cq,
            UIntPtr userData,
            out IntPtr outOp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_cancel(IntPtr op);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_op_release(IntPtr op);

        // ---- response ----------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort cosmos_response_status(IntPtr response);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosStatus cosmos_response_body(
            IntPtr response,
            out IntPtr outPtr,
            out UIntPtr outLen);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_response_free(IntPtr response);
    }
}
