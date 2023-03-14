//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Internal Cosmos headers to pass between classes/functions
    /// </summary>
    internal class AdditionalRequestHeaders
    {
        // Boolean to let the backend know if a query is utilizing the OptimisticDirectExecute pipeline.
        internal bool OptimisticDirectExecute { get; }

        // Boolean to let the backend know if continuations are expected for a certain query.
        internal bool IsContinuationExpected { get; }

        internal string ContentType { get; }

        // Boolean to let the backend know if a given operation is a query or not.
        internal bool IsQuery { get; }

        // GUID to enable backend to link multiple activityIds that belong to the same operation.
        internal Guid CorrelatedActivityId { get; }

        public AdditionalRequestHeaders(bool optimisticDirectExecute = false, bool isContinuationExpected = false, Guid correlatedActivityId = new Guid(), bool isQuery = true, string contentType = MediaTypes.QueryJson)
        {
            this.OptimisticDirectExecute = optimisticDirectExecute;
            this.IsContinuationExpected = isContinuationExpected;
            this.ContentType = contentType;
            this.IsQuery = isQuery;
            this.CorrelatedActivityId = correlatedActivityId;
        }
    }
}