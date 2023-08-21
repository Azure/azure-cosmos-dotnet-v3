//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMDateTimeLiteral : ClientQLLiteral
    {
        public ClientQLMDateTimeLiteral(int value)
            : base(ClientQLLiteralKind.MDateTime)
        {
            this.Value = value;
        }

        public int Value { get; }
    }
}