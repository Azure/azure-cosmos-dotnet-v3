//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLObjectCreateScalarExpression : QLScalarExpression
    {
        private const string Object = "Object";

        public QLObjectCreateScalarExpression(IReadOnlyList<QLObjectProperty> properties) 
            : base(QLScalarExpressionKind.ObjectCreate)
        {
            this.Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.ObjectKind = Object;
        }

        public IReadOnlyList<QLObjectProperty> Properties { get; }
        
        public string ObjectKind { get; }
    }
}