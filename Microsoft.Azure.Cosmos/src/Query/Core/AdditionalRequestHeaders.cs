﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;

    /// <summary>
    /// Captures additional headers to be sent in the web request.
    /// </summary>
    internal sealed class AdditionalRequestHeaders
    {
        /// <summary>
        /// Enable backend to link multiple activityIds that belong to the same operation.
        /// </summary>
        public Guid CorrelatedActivityId { get; }

        /// <summary>
        /// Let the backend know if continuations are expected for a certain query.
        /// </summary>
        public bool IsContinuationExpected { get; }

        /// <summary>
        /// Let the backend know if a query is utilizing the OptimisticDirectExecute pipeline.
        /// </summary>
        public bool OptimisticDirectExecute { get; }

        public AdditionalRequestHeaders(Guid correlatedActivityId, bool isContinuationExpected, bool optimisticDirectExecute)
        {
            this.CorrelatedActivityId = correlatedActivityId;
            this.IsContinuationExpected = isContinuationExpected;
            this.OptimisticDirectExecute = optimisticDirectExecute;
        }
    }
}