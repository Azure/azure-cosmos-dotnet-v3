//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLNumberLiteral : ClientQLLiteral
    {
        public ClientQLNumberLiteral(long value)
            : base(ClientQLLiteralKind.Number)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}