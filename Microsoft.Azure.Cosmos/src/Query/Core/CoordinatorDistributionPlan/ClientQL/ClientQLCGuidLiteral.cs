//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLCGuidLiteral : ClientQLLiteral
    {
        public ClientQLCGuidLiteral(System.Guid value)
            : base(ClientQLLiteralKind.CGuid)
        {
            this.Value = value;
        }

        public System.Guid Value { get; }
    }
}