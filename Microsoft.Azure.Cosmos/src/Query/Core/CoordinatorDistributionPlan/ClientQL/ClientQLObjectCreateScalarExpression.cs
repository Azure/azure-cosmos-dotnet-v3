//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLObjectCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLObjectCreateScalarExpression(IReadOnlyList<ClientQLObjectProperty> properties, ClientQLObjectKind objectKind) 
            : base(ClientQLScalarExpressionKind.ObjectCreate)
        {
            this.Properties = properties;
            this.ObjectKind = objectKind;
        }

        public IReadOnlyList<ClientQLObjectProperty> Properties { get; }
        
        public ClientQLObjectKind ObjectKind { get; }
    }

}