//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLObjectLiteral : QLLiteral
    {
        public QLObjectLiteral(IReadOnlyList<QLObjectLiteralProperty> properties)
            : base(QLLiteralKind.Object)
        {
            this.Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public IReadOnlyList<QLObjectLiteralProperty> Properties { get; }
    }
}