//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Request options for <see cref="ChangeFeedEstimator"/>.
    /// </summary>
    public sealed class ChangeFeedEstimatorRequestOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of estimations to return per page.
        /// </summary>
        /// <remarks>
        /// If this value is greater than the number of leases, all estimations will be returned in the first page.
        /// </remarks>
        public int? MaxItemCount { get; set; }
    }
}