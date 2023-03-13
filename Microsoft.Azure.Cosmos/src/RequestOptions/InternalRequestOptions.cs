//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Internal Cosmos query request options to pass between classes/functions
    /// </summary>
    internal class InternalRequestOptions
    {
        internal bool OptimisticDirectExecute { get; set; }
        internal bool IsContinuationExpected { get; set; }
        internal string ContentType { get; set; } = MediaTypes.QueryJson;
        internal bool IsQuery { get; set; } = true;
        internal Guid CorrelatedActivityId { get; set; }
    }
}