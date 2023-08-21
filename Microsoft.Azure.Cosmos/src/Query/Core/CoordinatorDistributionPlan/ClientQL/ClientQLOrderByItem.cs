//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLOrderByItem
    {
        public ClientQLOrderByItem(ClientQLScalarExpression expression, ClientQLSortOrder sortOrder)
        {
            this.Expression = expression;
            this.SortOrder = sortOrder;
        }

        public ClientQLScalarExpression Expression { get; }
        
        public ClientQLSortOrder SortOrder { get; }
    }
}