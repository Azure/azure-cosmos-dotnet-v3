// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

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
            if (autoUpgradeMaxThroughputIncrementPercentage.HasValue)
            {
                this.AutopilotAutoUpgradeProperties = new OfferAutoscaleAutoUpgradeProperties(autoUpgradeMaxThroughputIncrementPercentage.Value);
            }
            else
            {
                this.AutopilotAutoUpgradeProperties = null;
            }
        }

        /// <summary>
        /// The maximum throughput the autopilot will scale to.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotMaxThroughput, NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxThroughput { get; private set; }

        /// <summary>
        /// Scales the maximum through put automatically
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotAutoUpgradePolicy, NullValueHandling = NullValueHandling.Ignore)]
        public OfferAutoscaleAutoUpgradeProperties AutopilotAutoUpgradeProperties { get; private set; }

        internal string GetJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
