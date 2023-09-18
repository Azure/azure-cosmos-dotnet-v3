//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLObjectLiteralProperty
    {
        public QLObjectLiteralProperty(string name, QLLiteral literal)
        {
            this.Name = name;
            this.Literal = literal;
        }

        public string Name { get; }

        public QLLiteral Literal { get; }
    }
}