//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLInputEnumerableExpression : QLEnumerableExpression
    {
        public QLInputEnumerableExpression(string name) 
            : base(QLEnumerableExpressionKind.Input)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}