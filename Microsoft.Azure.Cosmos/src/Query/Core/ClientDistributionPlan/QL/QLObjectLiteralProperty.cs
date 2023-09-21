//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLObjectLiteralProperty
    {
        public QLObjectLiteralProperty(string name, QLLiteral literal)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Literal = literal ?? throw new ArgumentNullException(nameof(literal));
        }

        public string Name { get; }

        public QLLiteral Literal { get; }
    }
}