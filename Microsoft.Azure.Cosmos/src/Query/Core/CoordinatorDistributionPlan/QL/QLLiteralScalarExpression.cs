//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLLiteralScalarExpression : QLScalarExpression
    {
        public QLLiteralScalarExpression(QLLiteral literal)
           : base(QLScalarExpressionKind.Literal)
        {
            this.Literal = literal;
        }

        public QLLiteral Literal { get; }
    }

}