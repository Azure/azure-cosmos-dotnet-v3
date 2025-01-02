//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    internal class OperationMetricData
    {
        public OperationMetricData(string itemCount, double? requestCharge)
        {
            this.ItemCount = itemCount;
            this.RequestCharge = requestCharge;
        }

        public string ItemCount { get; }

        public double? RequestCharge { get; }
    }
}
