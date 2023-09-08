//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLObjectLiteralProperty
    {
        public ClientQLObjectLiteralProperty(string name, ClientQLLiteral literal)
        {
            this.Name = name;
            this.Literal = literal;
        }

        public string Name { get; }

        public ClientQLLiteral Literal { get; }
    }
}