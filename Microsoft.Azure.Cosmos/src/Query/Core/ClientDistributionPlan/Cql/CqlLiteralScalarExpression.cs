//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlLiteralScalarExpression : CqlScalarExpression
    {
        public CqlLiteralScalarExpression(CqlLiteral literal)
           : base(CqlScalarExpressionKind.Literal)
        {
            this.Literal = literal ?? throw new ArgumentNullException(nameof(literal));
        }

        public CqlLiteral Literal { get; }
    }
}