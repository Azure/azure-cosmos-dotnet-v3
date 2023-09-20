//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLStringLiteral : QLLiteral
    {
        public QLStringLiteral(string value)
            : base(QLLiteralKind.String)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Value { get; }
    }
}