//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class ClientDistributionPlan
    {
        public ClientDistributionPlan(QLEnumerableExpression clientQL)
        {
            this.ClientQL = clientQL ?? throw new ArgumentNullException(nameof(clientQL));
        }

        public QLEnumerableExpression ClientQL { get; }
    }
}