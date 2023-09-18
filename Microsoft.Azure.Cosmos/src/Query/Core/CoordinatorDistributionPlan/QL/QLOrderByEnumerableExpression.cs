//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLOrderByEnumerableExpression : QLEnumerableExpression
    {
        public QLOrderByEnumerableExpression(QLEnumerableExpression sourceExpression, QLVariable declaredVariable, IReadOnlyList<QLOrderByItem> items)
            : base(QLEnumerableExpressionKind.OrderBy)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.Items = items;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public QLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<QLOrderByItem> Items { get; }
    }

}