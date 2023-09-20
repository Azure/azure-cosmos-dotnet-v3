//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLInputEnumerableExpression : QLEnumerableExpression
    {
        public QLInputEnumerableExpression(string name) 
            : base(QLEnumerableExpressionKind.Input)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
    }
}