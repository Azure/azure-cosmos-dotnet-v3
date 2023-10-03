//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlObjectLiteralProperty
    {
        public CqlObjectLiteralProperty(string name, CqlLiteral literal)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Literal = literal ?? throw new ArgumentNullException(nameof(literal));
        }

        public string Name { get; }

        public CqlLiteral Literal { get; }
    }
}