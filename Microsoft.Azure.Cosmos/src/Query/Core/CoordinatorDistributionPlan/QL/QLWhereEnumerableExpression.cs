//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLWhereEnumerableExpression : QLEnumerableExpression
    {
        public QLWhereEnumerableExpression(QLEnumerableExpression sourceExpression)
            : base(QLEnumerableExpressionKind.Where)
        {
            this.SourceExpression = sourceExpression;
        }

        public QLEnumerableExpression SourceExpression { get; }
    }

}