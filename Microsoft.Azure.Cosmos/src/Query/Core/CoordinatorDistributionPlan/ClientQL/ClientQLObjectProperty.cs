//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLObjectProperty
    {
        public ClientQLObjectProperty(string name, ClientQLScalarExpression expression)
        {
            this.Name = name;
            this.Expression = expression;
        }

        public string Name { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }
}