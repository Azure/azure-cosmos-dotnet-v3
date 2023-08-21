//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLObjectCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLObjectCreateScalarExpression(List<ClientQLObjectProperty> properties, ClientQLObjectKind objectKind) 
            : base(ClientQLScalarExpressionKind.ObjectCreate)
        {
            this.Properties = properties;
            this.ObjectKind = objectKind;
        }

        public List<ClientQLObjectProperty> Properties { get; }
        
        public ClientQLObjectKind ObjectKind { get; }
    }

}