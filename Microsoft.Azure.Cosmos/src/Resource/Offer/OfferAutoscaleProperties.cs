// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class OfferAutoscaleProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        private OfferAutoscaleProperties()
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
        [JsonProperty(PropertyName = Constants.Properties.AutopilotMaxThroughput, NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxThroughput { get; private set; }

        /// <summary>
        /// Scales the maximum through put automatically
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotAutoUpgradePolicy, NullValueHandling = NullValueHandling.Ignore)]
        public OfferAutoscaleAutoUpgradeProperties AutoscaleAutoUpgradeProperties { get; private set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        internal string GetJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
