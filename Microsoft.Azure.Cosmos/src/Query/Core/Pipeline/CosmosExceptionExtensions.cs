// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;

    internal static class CosmosExceptionExtensions
    {
        public static bool IsPartitionSplitException(this Exception ex)
        {
            return ex is CosmosException cosmosException
                && (cosmosException.StatusCode == System.Net.HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
        }
    }
}
