// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    internal sealed class DistributionPlanPayload
    {
        public DistributionPlanPayload(string backendDistributionPlan, string clientDistributionPlan)
        {
            this.BackendDistributionPlan = backendDistributionPlan;
            this.ClientDistributionPlan = clientDistributionPlan;
        }

        public string BackendDistributionPlan { get; }

        public string ClientDistributionPlan { get; }
    }
}
