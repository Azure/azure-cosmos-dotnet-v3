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
        internal bool OptimisticDirectExecute { get; }

        internal bool IsContinuationExpected { get; }

        internal string ContentType { get; }

        internal bool IsQuery { get; }

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