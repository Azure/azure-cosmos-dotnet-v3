//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMDateTimeLiteral : ClientQLLiteral
    {
        public ClientQLMDateTimeLiteral(long value)
            : base(ClientQLLiteralKind.MDateTime)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}