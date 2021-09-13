// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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

        public class AutoscaleThroughputProperties
        {
            public AutoscaleThroughputProperties(int incrementPercent)
            {
                this.IncrementPercent = incrementPercent;
            }

            [JsonProperty(PropertyName = Constants.Properties.AutopilotThroughputPolicyIncrementPercent, NullValueHandling = NullValueHandling.Ignore)]
            public int IncrementPercent { get; private set; }

            /// <summary>
            /// This contains additional values for scenarios where the SDK is not aware of new fields. 
            /// This ensures that if resource is read and updated none of the fields will be lost in the process.
            /// </summary>
            [JsonExtensionData]
            internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        }
    }
}
