//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal abstract class CqlLiteral
    {
        protected CqlLiteral(CqlLiteralKind kind)
        {
            this.Kind = kind;
        }

        public CqlLiteralKind Kind { get; }
    }
}