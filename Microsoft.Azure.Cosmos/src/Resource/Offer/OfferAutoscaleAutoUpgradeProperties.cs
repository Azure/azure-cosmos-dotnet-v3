// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class OfferAutoscaleAutoUpgradeProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        private OfferAutoscaleAutoUpgradeProperties()
        {
        }

        internal OfferAutoscaleAutoUpgradeProperties(int incrementPercent)
        {
            this.ThroughputProperties = new AutoscaleThroughputProperties(incrementPercent);
        }

        [JsonProperty(PropertyName = Constants.Properties.AutopilotThroughputPolicy, NullValueHandling = NullValueHandling.Ignore)]
        public AutoscaleThroughputProperties ThroughputProperties { get; private set; }

        internal string GetJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public class AutoscaleThroughputProperties
        {
            public AutoscaleThroughputProperties(int incrementPercent)
            {
                this.IncrementPercent = incrementPercent;
            }

            [JsonProperty(PropertyName = Constants.Properties.AutopilotThroughputPolicyIncrementPercent, NullValueHandling = NullValueHandling.Ignore)]
            public int IncrementPercent { get; private set; }
        }
    }
}
