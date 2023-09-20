//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLLiteralScalarExpression : QLScalarExpression
    {
        public QLLiteralScalarExpression(QLLiteral literal)
           : base(QLScalarExpressionKind.Literal)
        {
            this.Literal = literal ?? throw new ArgumentNullException(nameof(literal));
        }

        public QLLiteral Literal { get; }
    }
}