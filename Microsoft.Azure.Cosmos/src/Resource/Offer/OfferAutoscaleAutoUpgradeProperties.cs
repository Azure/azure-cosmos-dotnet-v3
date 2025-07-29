// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal sealed class OfferAutoscaleAutoUpgradeProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        internal OfferAutoscaleAutoUpgradeProperties()
        {
        }

        internal OfferAutoscaleAutoUpgradeProperties(int incrementPercent)
        {
            this.ThroughputProperties = new AutoscaleThroughputProperties(incrementPercent);
        }

        [JsonPropertyName(Constants.Properties.AutopilotThroughputPolicy)]
        public AutoscaleThroughputProperties ThroughputProperties { get; private set; }

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

        public class AutoscaleThroughputProperties
        {
            public AutoscaleThroughputProperties(int incrementPercent)
            {
                this.IncrementPercent = incrementPercent;
            }

            [JsonPropertyName(Constants.Properties.AutopilotThroughputPolicyIncrementPercent)]
            public int IncrementPercent { get; private set; }

            /// <summary>
            /// This contains additional values for scenarios where the SDK is not aware of new fields. 
            /// This ensures that if resource is read and updated none of the fields will be lost in the process.
            /// </summary>
            [JsonExtensionData]
            internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }

        }
    }
}
