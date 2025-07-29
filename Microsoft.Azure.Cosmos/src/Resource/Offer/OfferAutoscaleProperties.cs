// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class OfferAutoscaleProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        internal OfferAutoscaleProperties()
        {
        }

        internal OfferAutoscaleProperties(
            int startingMaxThroughput,
            int? autoUpgradeMaxThroughputIncrementPercentage)
        {
            this.MaxThroughput = startingMaxThroughput;
            this.AutoscaleAutoUpgradeProperties = autoUpgradeMaxThroughputIncrementPercentage.HasValue
                ? new OfferAutoscaleAutoUpgradeProperties(autoUpgradeMaxThroughputIncrementPercentage.Value)
                : null;
        }

        /// <summary>
        /// The maximum throughput the autoscale will scale to.
        /// </summary>
        [JsonPropertyName(Constants.Properties.AutopilotMaxThroughput)]
        public int? MaxThroughput { get; private set; }

        /// <summary>
        /// Scales the maximum through put automatically
        /// </summary>
        [JsonPropertyName(Constants.Properties.AutopilotAutoUpgradePolicy)]
        public OfferAutoscaleAutoUpgradeProperties AutoscaleAutoUpgradeProperties { get; private set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }

        internal string GetJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
