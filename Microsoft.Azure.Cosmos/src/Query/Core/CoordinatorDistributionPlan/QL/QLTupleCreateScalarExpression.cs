//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLTupleCreateScalarExpression : QLScalarExpression
    {
        public QLTupleCreateScalarExpression(IReadOnlyList<QLScalarExpression> items) 
            : base(QLScalarExpressionKind.TupleCreate)
        {
            this.Items = items;
        }

        public IReadOnlyList<QLScalarExpression> Items { get; }
    }

}