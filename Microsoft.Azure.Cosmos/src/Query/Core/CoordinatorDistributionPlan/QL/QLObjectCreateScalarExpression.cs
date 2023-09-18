//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLObjectCreateScalarExpression : QLScalarExpression
    {
        public QLObjectCreateScalarExpression(IReadOnlyList<QLObjectProperty> properties, QLObjectKind objectKind) 
            : base(QLScalarExpressionKind.ObjectCreate)
        {
            this.Properties = properties;
            this.ObjectKind = objectKind;
        }

        public IReadOnlyList<QLObjectProperty> Properties { get; }
        
        public QLObjectKind ObjectKind { get; }
    }

}