//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLFunctionIdentifier
    {
        public QLFunctionIdentifier(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
    }
}