//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal class CqlNullLiteral : CqlLiteral
    {
        public static readonly CqlNullLiteral Singleton = new CqlNullLiteral();

        private CqlNullLiteral()
            : base(CqlLiteralKind.Null)
        {
        }
    }
}