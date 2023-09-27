//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    internal class CqlUndefinedLiteral : CqlLiteral
    {
        public static readonly CqlUndefinedLiteral Singleton = new CqlUndefinedLiteral();

        private CqlUndefinedLiteral()
            : base(CqlLiteralKind.Undefined)
        {
        }
    }
}