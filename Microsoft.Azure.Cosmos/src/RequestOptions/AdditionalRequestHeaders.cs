//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Capturing additional headers to be sent in the web request.
    /// </summary>
    internal sealed class AdditionalRequestHeaders
    {
        /// <summary>
        /// GUID to enable backend to link multiple activityIds that belong to the same operation.
        /// </summary>
        internal Guid CorrelatedActivityId { get; }

        /// <summary>
        /// Boolean to let the backend know if continuations are expected for a certain query.
        /// </summary>
        internal bool IsContinuationExpected { get; }

        /// <summary>
        /// Boolean to let the backend know if a query is utilizing the OptimisticDirectExecute pipeline.
        /// </summary>
        internal bool OptimisticDirectExecute { get; }

        public AdditionalRequestHeaders(Guid correlatedActivityId = default, bool isContinuationExpected = false, bool optimisticDirectExecute = false)
        {
            this.CorrelatedActivityId = correlatedActivityId;
            this.IsContinuationExpected = isContinuationExpected;
            this.OptimisticDirectExecute = optimisticDirectExecute;
        }
    }
}