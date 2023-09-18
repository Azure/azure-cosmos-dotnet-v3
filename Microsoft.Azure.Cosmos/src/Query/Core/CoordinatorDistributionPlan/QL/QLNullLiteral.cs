//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLNullLiteral : QLLiteral
    {
        public static readonly QLNullLiteral Singleton = new QLNullLiteral();

        private QLNullLiteral()
            : base(QLLiteralKind.Null)
        {
        }
    }
}