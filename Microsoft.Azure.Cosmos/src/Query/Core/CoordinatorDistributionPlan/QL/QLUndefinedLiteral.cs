//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLUndefinedLiteral : QLLiteral
    {
        public static readonly QLUndefinedLiteral Singleton = new QLUndefinedLiteral();

        private QLUndefinedLiteral()
            : base(QLLiteralKind.Undefined)
        {
        }
    }
}