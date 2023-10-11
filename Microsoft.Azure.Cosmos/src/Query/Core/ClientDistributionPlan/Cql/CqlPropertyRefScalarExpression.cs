//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlPropertyRefScalarExpression : CqlScalarExpression
    {
        public CqlPropertyRefScalarExpression(CqlScalarExpression expression, string propertyName) 
            : base(CqlScalarExpressionKind.PropertyRef)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }

        public CqlScalarExpression Expression { get; }
        
        public string PropertyName { get; }
    }
}