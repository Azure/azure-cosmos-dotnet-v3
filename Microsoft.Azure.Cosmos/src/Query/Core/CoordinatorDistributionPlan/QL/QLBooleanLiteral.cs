//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLBooleanLiteral : QLLiteral
    {
        public QLBooleanLiteral(bool value)
            : base(QLLiteralKind.Boolean)
        {
            this.Value = value;
        }

        public bool Value { get; }
    }
}