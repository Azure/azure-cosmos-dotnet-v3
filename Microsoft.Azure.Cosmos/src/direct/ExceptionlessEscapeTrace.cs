//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Reports exceptions that escaped past an exceptionless (<see cref="Res{T}"/>) boundary
    /// instead of being returned as a <c>Res</c> failure.
    /// </summary>
    /// <remarks>
    /// Statically proving that nothing below a <c>Try*</c> method throws is not tractable, and
    /// blanket try/catch is not acceptable. Instead the chokepoints that already catch an escaped
    /// exception report it, so the remaining problem areas are visible in production.
    ///
    /// The interesting case is an exception escaping a TRANSFORMATION point - somewhere that would
    /// have converted it into a different response to the client. That is what
    /// <c>retryable</c> distinguishes: a retryable escape means the retry policy never ran, so the
    /// client may have received a different status than it should have. A non-retryable escape
    /// only costs the throw.
    ///
    /// Every call site uses the single <see cref="EscapeTraceFormat"/> constant so all escapes
    /// share one trace hash and one Kusto query finds them regardless of which layer they came
    /// from. Treat the format string as load-bearing.
    ///
    /// Reporting is deliberately NOT throttled. Escapes are expected to be rare, and throttling
    /// would make the log misreport its own volume: a regression that made escapes common would
    /// present as a low trace count, which reads as "rare" rather than "suppressed". If escapes do
    /// become frequent the correct response is to disable the exceptionless feature flag, which
    /// removes the escapes at their source, rather than to log less about them.
    /// </remarks>
    internal static class ExceptionlessEscapeTrace
    {
        /// <summary>
        /// The one and only format string used for escape reporting. Do not inline, reword or
        /// reorder: all call sites must share this literal so they hash identically.
        /// </summary>
        private const string EscapeTraceFormat =
            "ExceptionlessEscape point={0} type={1} retryable={2} status={3} subStatus={4} message={5}";

        private const string Unknown = "unknown";

        // Escape point names. Kept as constants so the dimension stays a small, fixed set.
        public const string RequestRetryUtilityLoop = "RequestRetryUtility.TryProcessRequestAsync";
        public const string BackoffRetryUtilityLoop = "BackoffRetryUtility.TryExecuteAsync";
        public const string StoreReaderPrimaryResult = "StoreReader.TryGetResult";
        public const string ServiceInteropClientRawRequest = "ServiceInteropClient.TryProcessRawRequestAsync";
        public const string StoreClientUtilitiesExecute = "StoreClientUtilities.TryExecuteAndLogRequestAsync";
        public const string ApplicationStoreClientFacadeMessage = "ApplicationStoreClientFacade.TryProcessMessageAsync";

        /// <summary>
        /// Reports an exception that escaped an exceptionless boundary.
        /// </summary>
        /// <param name="point">One of the escape point constants on this type.</param>
        /// <param name="exception">The exception that escaped.</param>
        /// <param name="retryable">
        /// Whether the retry policy would have retried it, when that is known. Pass null where no
        /// policy is involved. A true value means the escape may have changed the client's answer.
        /// </param>
        public static void TraceEscape(string point, Exception exception, bool? retryable)
        {
            if (exception == null)
            {
                return;
            }

            string exceptionType = exception.GetType().Name;

            // No origin frame is reported. Exception.TargetSite does not exist on netstandard1.5
            // or net45, both of which Microsoft.Azure.Documents.Client multi-targets, and
            // Exception.StackTrace is banned by analyzer CDX1002 as harmful to performance.
            // Materialising a stack on a failure path would also work against the very cost this
            // feature exists to reduce. The escape point plus the exception type and message
            // localise to a subsystem, and the sanctioned EnableStackTraceLogging config remains
            // available when an exact frame is needed.
            string status = ExceptionlessEscapeTrace.Unknown;
            string subStatus = ExceptionlessEscapeTrace.Unknown;
            if (exception is DocumentClientException documentClientException)
            {
                HttpStatusCode? statusCode = documentClientException.StatusCode;
                status = statusCode.HasValue
                    ? ((int)statusCode.Value).ToString(CultureInfo.InvariantCulture)
                    : ExceptionlessEscapeTrace.Unknown;
                subStatus = ((int)documentClientException.GetSubStatus()).ToString(CultureInfo.InvariantCulture);
            }

            DefaultTrace.TraceWarning(
                ExceptionlessEscapeTrace.EscapeTraceFormat,
                point,
                exceptionType,
                retryable.HasValue ? (retryable.Value ? "true" : "false") : ExceptionlessEscapeTrace.Unknown,
                status,
                subStatus,
                exception.Message);
        }
    }
}
