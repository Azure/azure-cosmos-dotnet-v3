//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLOrderByItem
    {
        public QLOrderByItem(QLScalarExpression expression, QLSortOrder sortOrder)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.SortOrder = sortOrder;
        }

        public QLScalarExpression Expression { get; }
        
        public QLSortOrder SortOrder { get; }
    }
}