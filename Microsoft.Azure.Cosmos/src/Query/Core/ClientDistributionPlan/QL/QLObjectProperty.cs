//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLObjectProperty
    {
        public QLObjectProperty(string name, QLScalarExpression expression)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public string Name { get; }
        
        public QLScalarExpression Expression { get; }
    }
}