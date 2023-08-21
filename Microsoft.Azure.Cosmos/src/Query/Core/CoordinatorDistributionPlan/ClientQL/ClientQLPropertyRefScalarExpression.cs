//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLPropertyRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLPropertyRefScalarExpression(ClientQLScalarExpression expression, string propertyName) 
            : base(ClientQLScalarExpressionKind.PropertyRef)
        {
            this.Expression = expression;
            this.PropertyName = propertyName;
        }

        public ClientQLScalarExpression Expression { get; }
        
        public string PropertyName { get; }
    }

}