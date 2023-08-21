//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMNumberLiteral : ClientQLLiteral
    {
        public ClientQLMNumberLiteral(int value)
            : base(ClientQLLiteralKind.MNumber)
        {
            this.Value = value;
        }

        public int Value { get; }
    }
}