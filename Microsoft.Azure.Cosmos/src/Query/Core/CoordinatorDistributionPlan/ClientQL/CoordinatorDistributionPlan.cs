//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class CoordinatorDistributionPlan
    {
        public CoordinatorDistributionPlan(ClientQLExpression clientQL)
        {
            this.ClientQL = clientQL;
        }

        public ClientQLExpression ClientQL { get; }
    }
}