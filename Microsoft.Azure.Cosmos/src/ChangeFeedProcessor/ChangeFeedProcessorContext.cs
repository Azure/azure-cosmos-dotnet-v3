//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Context that is related to the set of delivered changes.
    /// </summary>
    public abstract class ChangeFeedProcessorContext
    {
        /// <summary>
        /// Gets the token representative of the current lease from which the changes come from.
        /// </summary>
        public abstract string LeaseToken { get; }

        /// <summary>
        /// Gets the diagnostics related to the service response.
        /// </summary>
        public abstract CosmosDiagnostics Diagnostics { get; }

        /// <summary>
        /// Gets the headers related to the service response that provided the changes.
        /// </summary>
        public abstract Headers Headers { get; }

        /// <summary>
        /// The <see cref="FeedRange"/> within the monitored container from which the changes emanated.
        /// </summary>
        public abstract FeedRange FeedRange { get; }
    }
}