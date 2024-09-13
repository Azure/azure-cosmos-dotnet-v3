//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class ClientDistributionPlan
    {
        public ClientDistributionPlan(CqlEnumerableExpression cql)
        {
            this.Cql = cql ?? throw new ArgumentNullException(nameof(cql));
        }

        public CqlEnumerableExpression Cql { get; }
    }
}