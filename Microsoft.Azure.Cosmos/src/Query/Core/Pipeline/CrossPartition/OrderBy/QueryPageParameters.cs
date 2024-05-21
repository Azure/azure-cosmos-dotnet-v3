// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class QueryPageParameters
    {
        public string ActivityId { get; }

        public Lazy<CosmosQueryExecutionInfo> CosmosQueryExecutionInfo { get; }

        public DistributionPlanSpec DistributionPlanSpec { get; }

        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; }

        public QueryPageParameters(
            string activityId,
            Lazy<CosmosQueryExecutionInfo> cosmosQueryExecutionInfo,
            DistributionPlanSpec distributionPlanSpec,
            IReadOnlyDictionary<string, string> additionalHeaders)
        {
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.CosmosQueryExecutionInfo = cosmosQueryExecutionInfo;
            this.DistributionPlanSpec = distributionPlanSpec;
            this.AdditionalHeaders = additionalHeaders;
        }
    }
}