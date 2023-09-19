//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class CoordinatorDistributionPlan
    {
        public CoordinatorDistributionPlan(QLEnumerableExpression clientQL)
        {
            this.ClientQL = clientQL;
        }

        public QLEnumerableExpression ClientQL { get; }
    }
}