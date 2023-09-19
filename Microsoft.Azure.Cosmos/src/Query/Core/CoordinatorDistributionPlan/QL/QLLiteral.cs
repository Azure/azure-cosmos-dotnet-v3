//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal abstract class QLLiteral
    {
        protected QLLiteral(QLLiteralKind kind)
        {
            this.Kind = kind;
        }

        public QLLiteralKind Kind { get; }
    }
}