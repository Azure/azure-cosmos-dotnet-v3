//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLOrderByItem
    {
        public QLOrderByItem(QLScalarExpression expression, QLSortOrder sortOrder)
        {
            this.Expression = expression;
            this.SortOrder = sortOrder;
        }

        public QLScalarExpression Expression { get; }
        
        public QLSortOrder SortOrder { get; }
    }
}