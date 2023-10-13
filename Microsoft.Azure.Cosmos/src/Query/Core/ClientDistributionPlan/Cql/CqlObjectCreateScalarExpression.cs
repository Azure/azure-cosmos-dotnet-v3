//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlObjectCreateScalarExpression : CqlScalarExpression
    {
        private const string Object = "Object";

        public CqlObjectCreateScalarExpression(IReadOnlyList<CqlObjectProperty> properties) 
            : base(CqlScalarExpressionKind.ObjectCreate)
        {
            this.Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.ObjectKind = Object;
        }

        public IReadOnlyList<CqlObjectProperty> Properties { get; }
        
        public string ObjectKind { get; }
    }
}