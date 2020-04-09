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

        internal OfferAutoscaleProperties(int maxThroughput)
        {
            this.MaxThroughput = maxThroughput;
            this.AutopilotAutoUpgradeProperties = null;
        }

        internal OfferAutoscaleProperties(
            int startingMaxThroughput,
            int autoUpgradeMaxThroughputIncrementPercentage)
        {
            this.MaxThroughput = startingMaxThroughput;
            this.AutopilotAutoUpgradeProperties = new OfferAutoscaleAutoUpgradeProperties(autoUpgradeMaxThroughputIncrementPercentage);
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
