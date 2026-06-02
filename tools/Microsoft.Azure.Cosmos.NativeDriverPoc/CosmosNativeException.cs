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
    /// payload exposed by <c>cosmos_error_t</c> per spec §3.5.2.
    /// </summary>
    /// <remarks>
    /// Construction COPIES every field out of native memory then frees
    /// the <c>cosmos_error_t*</c>. After construction this object has no
    /// live pointer into Rust-owned memory — safe to keep, serialize,
    /// store, or hand to user code.
    /// </remarks>
    internal sealed class CosmosNativeException : Exception
    {
        public CosmosErrorKind Kind { get; }
        public ushort HttpStatusCode { get; }

        /// <summary>Spec §3.5.2 — synthetic sub-status (e.g. 20008 for CLIENT_OPERATION_TIMEOUT). -1 if absent.</summary>
        public int SubStatus { get; }
        public byte[] ResponseBody { get; }
        public string? ActivityId { get; }
        public string? SessionToken { get; }
        public string? ETag { get; }
        public long RetryAfterMs { get; }
        public string? Backtrace { get; }

        public bool IsTransient { get; }
        public bool IsThrottled { get; }
        public bool IsNotFound { get; }
        public bool IsConflict { get; }
        public bool IsPreconditionFailed { get; }
        public bool IsTimeout { get; }
        public bool IsGone { get; }
        public bool IsServiceError { get; }

        /// <summary>
        /// Build the exception by copying all fields out of the native
        /// <c>cosmos_error_t*</c>. Does NOT free the handle — caller owns
        /// that decision (the pump frees it after constructing this).
        /// </summary>
        public CosmosNativeException(IntPtr error)
            : base(FormatMessage(error))
        {
            this.Kind = cosmos_error_kind(error);
            this.HttpStatusCode = cosmos_error_status_code(error);
            this.SubStatus = cosmos_error_sub_status(error);

            CosmosBytesView body = cosmos_error_response_body(error);
            this.ResponseBody = BytesViewToManaged(body);

            this.ActivityId = PtrToUtf8(cosmos_error_activity_id(error));
            this.SessionToken = PtrToUtf8(cosmos_error_session_token(error));
            this.ETag = PtrToUtf8(cosmos_error_etag(error));
            this.RetryAfterMs = cosmos_error_retry_after_ms(error);
            this.Backtrace = PtrToUtf8(cosmos_error_backtrace(error));

            this.IsTransient = cosmos_error_is_transient(error);
            this.IsThrottled = cosmos_error_is_throttled(error);
            this.IsNotFound = cosmos_error_is_not_found(error);
            this.IsConflict = cosmos_error_is_conflict(error);
            this.IsPreconditionFailed = cosmos_error_is_precondition_failed(error);
            this.IsTimeout = cosmos_error_is_timeout(error);
            this.IsGone = cosmos_error_is_gone(error);
            this.IsServiceError = cosmos_error_is_service_error(error);
        }

        private static string FormatMessage(IntPtr error)
        {
            if (error == IntPtr.Zero)
            {
                return "Cosmos native error: <null error handle>";
            }
            string? msg = PtrToUtf8(cosmos_error_message(error));
            CosmosErrorKind kind = cosmos_error_kind(error);
            ushort http = cosmos_error_status_code(error);
            int sub = cosmos_error_sub_status(error);
            return $"Cosmos native error [kind={kind}, http={http}, sub={sub}]: {msg ?? "(no message)"}";
        }

        private static byte[] BytesViewToManaged(CosmosBytesView v)
        {
            int len = (int)v.Len.ToUInt32();
            if (v.Data == IntPtr.Zero || len <= 0)
            {
                return Array.Empty<byte>();
            }
            byte[] copy = new byte[len];
            Marshal.Copy(v.Data, copy, 0, len);
            return copy;
        }
    }

    /// <summary>
    /// Successful response materialized out of the native
    /// <c>cosmos_response_t*</c>. All string + byte fields are
    /// COPIED out of native memory so the host can keep this past
    /// the native response handle's lifetime.
    /// </summary>
    internal sealed class CosmosNativeResponse
    {
        public ushort HttpStatusCode { get; }
        public double RequestCharge { get; }
        public string? ActivityId { get; }
        public string? SessionToken { get; }
        public string? ETag { get; }
        public string? ContinuationToken { get; }
        public byte[] Body { get; }

        public CosmosNativeResponse(
            ushort http, double ru,
            string? activityId, string? sessionToken,
            string? etag, string? continuation,
            byte[] body)
        {
            this.HttpStatusCode = http;
            this.RequestCharge = ru;
            this.ActivityId = activityId;
            this.SessionToken = sessionToken;
            this.ETag = etag;
            this.ContinuationToken = continuation;
            this.Body = body;
        }

        public string BodyAsString() => Encoding.UTF8.GetString(this.Body);
    }
}
