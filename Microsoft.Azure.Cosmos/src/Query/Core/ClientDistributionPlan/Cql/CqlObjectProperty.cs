//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlObjectProperty
    {
        public CqlObjectProperty(string name, CqlScalarExpression expression)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public string Name { get; }
        
        public CqlScalarExpression Expression { get; }
    }
}