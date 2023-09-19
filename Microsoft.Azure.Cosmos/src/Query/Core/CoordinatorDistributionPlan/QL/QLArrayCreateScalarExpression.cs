//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLArrayCreateScalarExpression : QLScalarExpression
    {
        public QLArrayCreateScalarExpression(QLArrayKind arrayKind, IReadOnlyList<QLScalarExpression> items) 
            : base(QLScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = arrayKind;
            this.Items = items;
        }

        public QLArrayKind ArrayKind { get; }
        
        public IReadOnlyList<QLScalarExpression> Items { get; }
    }
}