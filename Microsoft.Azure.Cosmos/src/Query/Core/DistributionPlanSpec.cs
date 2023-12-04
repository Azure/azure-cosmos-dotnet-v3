// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;

    internal sealed class DistributionPlanSpec
    {
        public DistributionPlanSpec(string backendDistributionPlan, string clientDistributionPlan)
        {
            if (string.IsNullOrEmpty(backendDistributionPlan))
            {
                throw new ArgumentException("Backend distribution plan cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(clientDistributionPlan))
            {
                throw new ArgumentException("Client distribution plan cannot be null or empty.");
            }

            this.BackendDistributionPlan = backendDistributionPlan;
            this.ClientDistributionPlan = clientDistributionPlan;
        }

        public string BackendDistributionPlan { get; }

        public string ClientDistributionPlan { get; }
    }
}
