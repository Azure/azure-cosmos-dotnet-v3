//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System;
    using System.Collections.Generic;

    internal class ClientQLBinaryLiteral : ClientQLLiteral
    {
        public ClientQLBinaryLiteral(byte[] value)
            : base(ClientQLLiteralKind.Binary)
        {
            this.Value = value;
        }

        public byte[] Value { get; }
    }
}