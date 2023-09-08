//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLCNumberLiteral : ClientQLLiteral
    {
        public ClientQLCNumberLiteral(long value)
            : base(ClientQLLiteralKind.CNumber)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}