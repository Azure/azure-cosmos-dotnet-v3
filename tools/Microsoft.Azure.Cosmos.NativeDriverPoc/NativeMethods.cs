// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Raw P/Invoke surface for <c>azurecosmosdriver.dll</c> — the cdylib
    /// produced by the <c>azure_data_cosmos_driver_native</c> crate specced
    /// in PR https://github.com/Azure/azure-sdk-for-rust/pull/4461.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every signature in this file mirrors a normative section of
    /// <c>NATIVE_WRAPPER_SPEC.md</c>. The cross-references are tagged in
    /// the comment above each DllImport group so reviewers can audit
    /// each binding against the spec without context-switching.
    /// </para>
    /// <para>
    /// Scope: this V2 binding covers the Phase 0/1/2/3/4/5/6 surface
    /// needed for single-item CRUD against the emulator. Pager (Phase 8),
    /// diagnostics (Phase 7), batch (Phase 9), credential callbacks, and
    /// the full <c>cosmos_operation_options_*</c> setter family are
    /// intentionally NOT bound here — they are additive and can be
    /// layered in without rewriting any existing binding.
    /// </para>
    /// <para>
    /// Spec ambiguity note: several entry points declare a
    /// <c>cosmos_error_t *out_error</c> trailing parameter (single
    /// pointer). The <c>cosmos_error_t</c> type is opaque, so the only
    /// sane interpretation is that the caller passes an address to a
    /// pointer slot and the wrapper writes a fresh, caller-owned
    /// <c>cosmos_error_t*</c> into it (i.e. <c>cosmos_error_t **</c>
    /// semantics). All bindings below use <c>out IntPtr outError</c>,
    /// which matches that interpretation on the wire. Flag for spec-PR
    /// comment.
    /// </para>
    /// </remarks>
    internal static class NativeMethods
    {
        public const string LibraryName = "azurecosmosdriver";

        // -----------------------------------------------------------------
        // Enums — mirror NATIVE_WRAPPER_SPEC.md §3.5.1, §3.5.2, §3.6.1
        // -----------------------------------------------------------------

        /// <summary>Spec §3.5.1 — coarse return-value code on every fallible C function.</summary>
        public enum CosmosErrorCode : int
        {
            Success = 0,
            // 1..=999    : FFI / argument validation (inherited from PR #2906)
            // 1001..=1999: auth / conversion (inherited)
            // 2001..=2999: Cosmos-specific (inherited)
            // 3001..=3999: FFI plumbing (inherited)
            DriverNotInitialized = 4002,
            InvalidAccountReference = 4003,
            InvalidPartitionKey = 4004,
            OperationConsumed = 4005,
            ResponseConsumed = 4006,
            FeedExhausted = 4007,
            PreconditionAlreadySet = 4008,
            UnsupportedOperationForMutator = 4009,
            InvalidHeaderName = 4010,
            QueueShutdown = 4011,
            OperationCancelled = 4012,
            QueueFull = 4013,
            InvalidOptionValue = 4014,
            RuntimeBuildFailed = 4015,

            // 5xxx — non-fatal warnings (success path, advisory error populated)
            OptionsIgnoredOnCacheHit = 5001,
        }

        /// <summary>Spec §3.5.2 — kind taxonomy from <c>azure_data_cosmos::Error</c>.</summary>
        public enum CosmosErrorKind : int
        {
            Service = 0,
            Transport = 1,
            Client = 2,
            Authentication = 3,
            Serialization = 4,
            Configuration = 5,
            Unknown = 255,
        }

        /// <summary>Spec §3.6.1 — exactly one of OK / ERROR / CANCELLED per completion.</summary>
        public enum CosmosCompletionOutcome : int
        {
            Ok = 0,
            Error = 1,
            Cancelled = 2,
            Unknown = 255,
        }

        /// <summary>Spec §3.6.2 — lock-free poll on the in-flight operation handle.</summary>
        public enum CosmosOperationHandleState : int
        {
            InFlight = 0,
            Completed = 1,
            Failed = 2,
            Cancelled = 3,
        }

        /// <summary>Spec §3.1.3 — distinguishes wait-returned-NULL reasons.</summary>
        public enum CosmosCqState : int
        {
            Running = 0,
            Shutdown = 1,
            Drained = 2,
        }

        // -----------------------------------------------------------------
        // Structs — passed BY VALUE across the ABI (spec §3.3)
        // -----------------------------------------------------------------

        /// <summary>
        /// Spec §3.3 — caller-owned input view OR borrowed output view.
        /// Layout is published because this is pass-by-value. 16 bytes on
        /// 64-bit; returned from <see cref="cosmos_response_body"/> and
        /// <see cref="cosmos_error_response_body"/>; passed by value into
        /// <see cref="cosmos_operation_with_body"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CosmosBytesView
        {
            public IntPtr Data;   // const uint8_t*
            public UIntPtr Len;   // size_t

            public static CosmosBytesView Empty => new CosmosBytesView { Data = IntPtr.Zero, Len = UIntPtr.Zero };
        }

        // -----------------------------------------------------------------
        // Runtime — spec §4.1 (CosmosDriverRuntimeBuilder mirror)
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_runtime_builder_new();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_runtime_builder_free(IntPtr builder);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_worker_threads(
            IntPtr builder, uint workerThreads);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_thread_name_prefix(
            IntPtr builder, [MarshalAs(UnmanagedType.LPUTF8Str)] string prefix);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_user_agent_suffix(
            IntPtr builder, [MarshalAs(UnmanagedType.LPUTF8Str)] string suffix);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_workload_id(
            IntPtr builder, ulong workloadId);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_correlation_id(
            IntPtr builder, [MarshalAs(UnmanagedType.LPUTF8Str)] string correlationId);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_runtime_builder_with_allow_emulator_invalid_certs(
            IntPtr builder, [MarshalAs(UnmanagedType.U1)] bool allow);

        /// <summary>
        /// Spec §4.1 — consumes the builder. On failure the builder is
        /// freed and <paramref name="outRuntime"/> is left NULL.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_runtime_builder_build(
            IntPtr builder,
            out IntPtr outRuntime,
            out IntPtr outError);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_runtime_free(IntPtr runtime);

        // -----------------------------------------------------------------
        // Completion queue — spec §3.1
        // -----------------------------------------------------------------

        /// <summary>Spec §3.1.2 — passed by value into <c>cosmos_cq_create</c>.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CosmosCqOptions
        {
            public uint CapacityHint;
            public uint MaxCapacity;
            [MarshalAs(UnmanagedType.U1)]
            public bool IncludeErrorDetails;

            public static CosmosCqOptions Default => new CosmosCqOptions
            {
                CapacityHint = 1024,
                MaxCapacity = 0,
                IncludeErrorDetails = true,
            };
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_cq_create(IntPtr runtime, in CosmosCqOptions options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_cq_free(IntPtr cq);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_cq_shutdown(IntPtr cq);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosCqState cosmos_cq_state(IntPtr cq);

        /// <summary>
        /// Spec §3.1.3 — returns NULL on timeout / shutdown / drained /
        /// spurious wake (distinguish via <see cref="cosmos_cq_state"/>),
        /// non-NULL <c>cosmos_completion_t*</c> otherwise. Caller MUST
        /// free with <see cref="cosmos_completion_free"/>.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_cq_wait(IntPtr cq, uint timeoutMs);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_cq_try_wait(IntPtr cq);

        /// <summary>
        /// Spec §3.1.3 — batched wait. Drains up to <paramref name="maxCount"/>
        /// completions; caller MUST free each one. Recommended for
        /// single-consumer receive loops at high throughput.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint cosmos_cq_wait_batch(
            IntPtr cq,
            [Out] IntPtr[] outCompletions,
            uint maxCount,
            uint timeoutMs);

        // -----------------------------------------------------------------
        // Completion record — spec §3.6.1
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosCompletionOutcome cosmos_completion_outcome(IntPtr completion);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_user_data(IntPtr completion);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_op_handle(IntPtr completion);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_completion_status(IntPtr completion);

        /// <summary>
        /// Spec §3.6.1 — true iff cancel was requested before completion
        /// was posted, regardless of eventual outcome. Lets host SDKs
        /// distinguish "cancel won the race" from "natural completion
        /// arrived while cancel was pending".
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_completion_was_cancel_requested(IntPtr completion);

        /// <summary>Spec §3.6.1 — transfers ownership; caller frees via <see cref="cosmos_response_free"/>.</summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_take_response(IntPtr completion);

        /// <summary>Spec §3.6.1 — borrowed; lifetime = until <see cref="cosmos_completion_free"/>.</summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_response(IntPtr completion);

        /// <summary>Spec §3.6.1 — transfers ownership; caller frees via <see cref="cosmos_error_free"/>.</summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_take_error(IntPtr completion);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_completion_error(IntPtr completion);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_completion_free(IntPtr completion);

        // -----------------------------------------------------------------
        // Operation handle — spec §3.6.2
        // -----------------------------------------------------------------

        /// <summary>Spec §3.6.2 — request cooperative cancellation. Idempotent.</summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_operation_handle_cancel(IntPtr opHandle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosOperationHandleState cosmos_operation_handle_state(IntPtr opHandle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_operation_handle_free(IntPtr opHandle);

        // -----------------------------------------------------------------
        // Account / Database / Container references — spec §4.3
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_account_ref_with_master_key(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string endpoint,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
            out IntPtr outAccount,
            out IntPtr outError);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_account_ref_clone(
            IntPtr account, out IntPtr outClone);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_account_ref_free(IntPtr account);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_database_ref_create(
            IntPtr account,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string databaseId,
            out IntPtr outDatabase);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_database_ref_free(IntPtr database);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_container_ref_create(
            IntPtr database,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string containerId,
            out IntPtr outContainer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_container_ref_free(IntPtr container);

        // -----------------------------------------------------------------
        // Driver — spec §4.4
        // -----------------------------------------------------------------

        /// <summary>
        /// Spec §4.4 — synchronous convenience for startup-init paths
        /// (submits to a private internal queue and drains one completion).
        /// Cache-hit advisory surfaces via the return code +
        /// <paramref name="outError"/>.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_driver_get_or_create_blocking(
            IntPtr runtime,
            IntPtr account,
            IntPtr options,           // cosmos_driver_options_t* — nullable; pass IntPtr.Zero for defaults
            out IntPtr outDriver,
            out IntPtr outError);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_driver_free(IntPtr driver);

        // -----------------------------------------------------------------
        // Partition key — spec §4.5
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_partition_key_from_string(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
            out IntPtr outPk);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_partition_key_free(IntPtr pk);

        // -----------------------------------------------------------------
        // Operations — spec §4.6 (CRUD factories only for this POC)
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_operation_read_item(
            IntPtr container,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr partitionKey,
            out IntPtr outOp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_operation_create_item(
            IntPtr container,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr partitionKey,
            out IntPtr outOp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_operation_replace_item(
            IntPtr container,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr partitionKey,
            out IntPtr outOp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_operation_upsert_item(
            IntPtr container,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr partitionKey,
            out IntPtr outOp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CosmosErrorCode cosmos_operation_delete_item(
            IntPtr container,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string itemId,
            IntPtr partitionKey,
            out IntPtr outOp);

        /// <summary>
        /// Spec §4.6.2 — body is COPIED into wrapper-owned storage before
        /// returning. Caller may release / overwrite the source memory
        /// immediately after SUCCESS, including before the eventual submit.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_operation_with_body(
            IntPtr op, CosmosBytesView body);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_operation_free(IntPtr op);

        // -----------------------------------------------------------------
        // Submission — spec §4.7
        // -----------------------------------------------------------------

        /// <summary>
        /// Spec §4.7 — Pattern B async submit. Returns NULL on pre-flight
        /// rejection (writes coarse code to <paramref name="outPreError"/>);
        /// returns the in-flight handle on accepted submission. Runtime
        /// failures arrive later as a completion with outcome = ERROR.
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_driver_submit(
            IntPtr driver,
            IntPtr op,
            IntPtr options,                 // cosmos_operation_options_t* — nullable
            IntPtr cq,
            IntPtr userData,                // void* — opaque, round-tripped to completion
            out CosmosErrorCode outPreError);

        // -----------------------------------------------------------------
        // Response — spec §4.7
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort cosmos_response_status_code(IntPtr response);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern double cosmos_response_request_charge(IntPtr response);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_response_activity_id(
            IntPtr response, out IntPtr outStr);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_response_session_token(
            IntPtr response, out IntPtr outStrOrNull);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_response_etag(
            IntPtr response, out IntPtr outStrOrNull);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorCode cosmos_response_continuation_token(
            IntPtr response, out IntPtr outStrOrNull);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosBytesView cosmos_response_body(IntPtr response);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_response_free(IntPtr response);

        // -----------------------------------------------------------------
        // Rich error — spec §3.5.2
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosErrorKind cosmos_error_kind(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort cosmos_error_status_code(IntPtr error);

        /// <summary>Spec §3.5.2 — -1 if absent; otherwise non-negative (synthetic codes per PR #4442).</summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int cosmos_error_sub_status(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_error_message(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern CosmosBytesView cosmos_error_response_body(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_error_activity_id(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_error_session_token(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_error_etag(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long cosmos_error_retry_after_ms(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_error_backtrace(IntPtr error);

        // Predicates — spec §3.5.2

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_transient(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_throttled(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_not_found(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_conflict(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_precondition_failed(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_timeout(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_gone(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool cosmos_error_is_service_error(IntPtr error);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_error_free(IntPtr error);

        // -----------------------------------------------------------------
        // Bytes — spec §3.3 (opaque handle accessors; used when a caller
        // takes ownership via cosmos_response_into_body, not exercised
        // in this v1 POC but bound for completeness)
        // -----------------------------------------------------------------

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cosmos_bytes_data(IntPtr bytes);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr cosmos_bytes_len(IntPtr bytes);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void cosmos_bytes_free(IntPtr bytes);

        // -----------------------------------------------------------------
        // Common helper — borrowed C string → managed String
        // -----------------------------------------------------------------

        /// <summary>Marshal a borrowed UTF-8 C string into a managed <see cref="string"/>; returns null for NULL.</summary>
        public static string? PtrToUtf8(IntPtr ptr) =>
            ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
