//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLArrayCreateScalarExpression : QLScalarExpression
    {
        private const string Array = "Array";

        public QLArrayCreateScalarExpression(IReadOnlyList<QLScalarExpression> items) 
            : base(QLScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = Array;
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public string ArrayKind { get; }
        
        public IReadOnlyList<QLScalarExpression> Items { get; }
    }
}