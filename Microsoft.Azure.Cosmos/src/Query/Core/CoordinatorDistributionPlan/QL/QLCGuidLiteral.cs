//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLCGuidLiteral : QLLiteral
    {
        public QLCGuidLiteral(System.Guid value)
            : base(QLLiteralKind.CGuid)
        {
            this.Value = value;
        }

        public System.Guid Value { get; }
    }
}