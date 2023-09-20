//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class CoordinatorDistributionPlan
    {
        public CoordinatorDistributionPlan(QLEnumerableExpression clientQL)
        {
            this.ClientQL = clientQL ?? throw new ArgumentNullException(nameof(clientQL));
        }

        public QLEnumerableExpression ClientQL { get; }
    }
}