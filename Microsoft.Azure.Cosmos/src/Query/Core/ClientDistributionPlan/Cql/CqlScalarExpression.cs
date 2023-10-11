//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal abstract class CqlScalarExpression
    {
        protected CqlScalarExpression(CqlScalarExpressionKind kind)
        {
            this.Kind = kind;
        }

        public CqlScalarExpressionKind Kind { get; }
    }
}