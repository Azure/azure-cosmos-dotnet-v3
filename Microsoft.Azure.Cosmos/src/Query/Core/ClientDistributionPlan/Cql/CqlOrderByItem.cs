//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlOrderByItem
    {
        public CqlOrderByItem(CqlScalarExpression expression, CqlSortOrder sortOrder)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.SortOrder = sortOrder;
        }

        public CqlScalarExpression Expression { get; }
        
        public CqlSortOrder SortOrder { get; }
    }
}