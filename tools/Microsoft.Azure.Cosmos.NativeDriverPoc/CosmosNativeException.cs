// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// Strongly-typed exception thrown when a Cosmos completion arrives
    /// with <see cref="CosmosCompletionOutcome.Error"/>. Mirrors the rich
    /// payload exposed by <c>cosmos_error_t</c> per
    /// <c>azurecosmosdriver.h</c> lines 1082-1137.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construction COPIES every accessor result out of native memory.
    /// After construction this object has no live pointer into Rust-owned
    /// memory — safe to keep, serialize, store, or hand to user code.
    /// </para>
    /// <para>
    /// Big change vs the earlier draft: there is NO <c>cosmos_error_kind</c>
    /// accessor and NO per-failure-class <c>is_*</c> predicates
    /// (IsThrottled / IsNotFound / etc.). The "kind" lives in
    /// <see cref="CoarseCode"/> — compare against the enum bands
    /// (2xxx = wire-mapped HTTP, 3xxx = client-side, 4xxx = wrapper-fatal).
    /// The only predicate exposed by the actual ABI is
    /// <see cref="IsFromWire"/>.
    /// </para>
    /// </remarks>
    internal sealed class CosmosNativeException : Exception
    {
        public CosmosErrorCode CoarseCode { get; }
        public ushort HttpStatusCode { get; }
        public int SubStatus { get; }
        public bool IsFromWire { get; }
        public string? ActivityId { get; }
        public string? SessionToken { get; }
        public string? ETag { get; }
        public long RetryAfterMs { get; }
        public string? Backtrace { get; }

        // Convenience predicates derived from CoarseCode — these are the
        // ABI-stable replacement for the spec-draft cosmos_error_is_*
        // predicates that were dropped between spec and implementation.
        public bool IsNotFound => this.CoarseCode == CosmosErrorCode.NotFound;
        public bool IsConflict => this.CoarseCode == CosmosErrorCode.Conflict;
        public bool IsThrottled => this.CoarseCode == CosmosErrorCode.Throttled;
        public bool IsTimeout =>
            this.CoarseCode == CosmosErrorCode.Timeout ||
            this.CoarseCode == CosmosErrorCode.ClientOperationTimeout;
        public bool IsGone => this.CoarseCode == CosmosErrorCode.Gone;
        public bool IsPreconditionFailed => this.CoarseCode == CosmosErrorCode.PreconditionFailed;
        public bool IsServiceError =>
            this.CoarseCode == CosmosErrorCode.ServiceError ||
            this.CoarseCode == CosmosErrorCode.ServiceUnavailable;
        public bool IsTransient =>
            this.IsThrottled ||
            this.IsTimeout ||
            this.IsGone ||
            this.IsServiceError ||
            this.CoarseCode == CosmosErrorCode.TransportFailure;

        /// <summary>
        /// Build the exception from values already copied out of the by-value
        /// <c>cosmos_completion_t</c> (merged PR #4515 unified the response /
        /// error payloads into the completion struct, so the pump reads the
        /// error fields inline rather than via a separate <c>cosmos_error_t</c>).
        /// </summary>
        public CosmosNativeException(
            CosmosErrorCode coarseCode,
            ushort httpStatusCode,
            int subStatus,
            bool isFromWire,
            string? message,
            string? activityId,
            string? sessionToken,
            string? etag,
            long retryAfterMs,
            string? backtrace)
            : base(FormatMessage(coarseCode, httpStatusCode, subStatus, message))
        {
            this.CoarseCode = coarseCode;
            this.HttpStatusCode = httpStatusCode;
            this.SubStatus = subStatus;
            this.IsFromWire = isFromWire;
            this.ActivityId = activityId;
            this.SessionToken = sessionToken;
            this.ETag = etag;
            this.RetryAfterMs = retryAfterMs;
            this.Backtrace = backtrace;
        }

        private static string FormatMessage(
            CosmosErrorCode coarseCode, ushort http, int sub, string? message) =>
            $"Cosmos native error [code={coarseCode}, http={http}, sub={sub}]: {message ?? "(no message)"}";

        /// <summary>
        /// Build the exception by copying accessor results out of the
        /// native <c>cosmos_error_t*</c>. Does NOT free the handle — the
        /// caller owns that decision. Still used by the blocking bootstrap
        /// path (<c>get_or_create</c> / <c>resolve_container</c>), which
        /// returns a rich <c>cosmos_error_t</c> out-param.
        /// </summary>
        public CosmosNativeException(IntPtr error, CosmosErrorCode coarseCode)
            : base(FormatMessage(error, coarseCode))
        {
            this.CoarseCode = coarseCode;
            this.HttpStatusCode = error == IntPtr.Zero ? (ushort)0 : cosmos_error_status_code(error);
            this.SubStatus = error == IntPtr.Zero ? -1 : cosmos_error_sub_status(error);
            this.IsFromWire = error != IntPtr.Zero && cosmos_error_is_from_wire(error);
            this.ActivityId = error == IntPtr.Zero ? null : PtrToUtf8(cosmos_error_activity_id(error));
            this.SessionToken = error == IntPtr.Zero ? null : PtrToUtf8(cosmos_error_session_token(error));
            this.ETag = error == IntPtr.Zero ? null : PtrToUtf8(cosmos_error_etag(error));
            this.RetryAfterMs = error == IntPtr.Zero ? -1 : cosmos_error_retry_after_ms(error);
            this.Backtrace = error == IntPtr.Zero ? null : PtrToUtf8(cosmos_error_backtrace(error));
        }

        private static string FormatMessage(IntPtr error, CosmosErrorCode coarseCode)
        {
            if (error == IntPtr.Zero)
            {
                return $"Cosmos native error [code={coarseCode}]: <null error handle>";
            }
            string? msg = PtrToUtf8(cosmos_error_message(error));
            ushort http = cosmos_error_status_code(error);
            int sub = cosmos_error_sub_status(error);
            return $"Cosmos native error [code={coarseCode}, http={http}, sub={sub}]: {msg ?? "(no message)"}";
        }
    }

    /// <summary>
    /// Successful response materialized out of the native
    /// <c>cosmos_response_t*</c>. All string + byte fields are copied
    /// out of native memory.
    /// </summary>
    internal sealed class CosmosNativeResponse
    {
        public ushort HttpStatusCode { get; }
        public double RequestCharge { get; }
        public string? ActivityId { get; }
        public string? SessionToken { get; }
        public string? ETag { get; }

        /// <summary>
        /// Raw server-header continuation (from
        /// <c>cosmos_response_continuation_token</c>) — valid only for
        /// trivial single-partition reads. For feed pagination prefer
        /// <see cref="NextContinuation"/>, which is the planner-derived
        /// token (azurecosmosdriver.h §1683-1697).
        /// </summary>
        public string? ContinuationToken { get; }

        /// <summary>
        /// Next-page continuation token for feed responses
        /// (<c>cosmos_response_next_continuation</c>). NULL on the last
        /// page, on non-feed responses, and on degenerate end-of-stream
        /// responses (status code 0). Pass back as the
        /// <c>continuationToken</c> argument to the next
        /// <c>QueryItemsPageAsync</c> call to fetch the following page.
        /// </summary>
        public string? NextContinuation { get; }

        public byte[] Body { get; }

        public CosmosNativeResponse(
            ushort http, double ru,
            string? activityId, string? sessionToken,
            string? etag, string? continuation, string? nextContinuation,
            byte[] body)
        {
            this.HttpStatusCode = http;
            this.RequestCharge = ru;
            this.ActivityId = activityId;
            this.SessionToken = sessionToken;
            this.ETag = etag;
            this.ContinuationToken = continuation;
            this.NextContinuation = nextContinuation;
            this.Body = body;
        }

        public string BodyAsString() => Encoding.UTF8.GetString(this.Body);
    }
}
