//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLBinaryLiteral : QLLiteral
    {
        public QLBinaryLiteral(byte[] value)
            : base(QLLiteralKind.Binary)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public byte[] Value { get; }
    }
}