//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLPropertyRefScalarExpression : QLScalarExpression
    {
        public QLPropertyRefScalarExpression(QLScalarExpression expression, string propertyName) 
            : base(QLScalarExpressionKind.PropertyRef)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }

        public QLScalarExpression Expression { get; }
        
        public string PropertyName { get; }
    }
}