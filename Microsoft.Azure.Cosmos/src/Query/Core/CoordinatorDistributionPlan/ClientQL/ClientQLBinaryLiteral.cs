//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System;
    using System.Collections.Generic;

    internal class ClientQLBinaryLiteral : ClientQLLiteral
    {
        public ClientQLBinaryLiteral(IReadOnlyList<BinaryData> value)
            : base(ClientQLLiteralKind.Binary)
        {
            this.Value = value;
        }

        public IReadOnlyList<BinaryData> Value { get; }
    }
}