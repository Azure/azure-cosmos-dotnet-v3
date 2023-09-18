//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLObjectProperty
    {
        public QLObjectProperty(string name, QLScalarExpression expression)
        {
            this.Name = name;
            this.Expression = expression;
        }

        public string Name { get; }
        
        public QLScalarExpression Expression { get; }
    }
}