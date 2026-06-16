//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Neutral, dependency-free classifier for the shared metadata regional-failure
    /// list. This is the single source of truth consumed by BOTH the lower-level
    /// <see cref="MetadataRequestThrottleRetryPolicy"/> (endpoint-advance retries) and
    /// the higher-level cold-start <see cref="MetadataHedgingStrategy"/>
    /// (acceptable-winner gating). It lives in its own type — rather than on the
    /// hedging strategy — so the retry policy does not depend on the hedging type for
    /// its core failure classification. See
    /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §5.7.2.
    /// </summary>
    internal static class MetadataRegionalFailureClassifier
    {
        /// <summary>
        /// Returns <c>true</c> iff the response or exception represents a regional failure
        /// that should advance a metadata retry/hedge to a different preferred location.
        /// </summary>
        /// <param name="statusCode">HTTP status code from the response, or <c>null</c> if no
        /// response was produced (transport-level failure).</param>
        /// <param name="subStatus">Cosmos sub-status code from the response.</param>
        /// <param name="exception">Exception observed instead of (or in addition to) the
        /// response, or <c>null</c>.</param>
        /// <param name="callerToken">The caller-supplied cancellation token. Used to
        /// distinguish a user-initiated cancellation (not a regional failure) from a
        /// non-user cancellation surfaced by the HTTP timeout policy (regional failure).</param>
        internal static bool IsRegionalFailure(
            HttpStatusCode? statusCode,
            SubStatusCodes subStatus,
            Exception exception,
            CancellationToken callerToken)
        {
            if (exception is HttpRequestException)
            {
                return true;
            }

            if (exception is OperationCanceledException && !callerToken.IsCancellationRequested)
            {
                return true;
            }

            if (statusCode == null)
            {
                return false;
            }

            switch (statusCode.Value)
            {
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.InternalServerError:
                    return true;
                case HttpStatusCode.Gone:
                    return subStatus == SubStatusCodes.LeaseNotFound;
                case HttpStatusCode.Forbidden:
                    return subStatus == SubStatusCodes.DatabaseAccountNotFound;
                default:
                    return false;
            }
        }
    }
}
