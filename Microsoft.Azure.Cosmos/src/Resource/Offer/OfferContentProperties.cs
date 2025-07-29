// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class OfferContentProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        internal OfferContentProperties()
        {
        }

        internal OfferContentProperties(int manualThroughput)
        {
            this.OfferThroughput = manualThroughput;
            this.OfferAutoscaleSettings = null;
        }

        internal OfferContentProperties(OfferAutoscaleProperties autoscaleProperties)
        {
            this.OfferThroughput = null;
            this.OfferAutoscaleSettings = autoscaleProperties ?? throw new ArgumentNullException(nameof(autoscaleProperties));
        }

        /// <summary>
        /// Represents customizable throughput chosen by user for his collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.OfferThroughput)]
        public int? OfferThroughput { get; private set; }

        /// <summary>
        /// Represents customizable throughput chosen by user for his collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.AutopilotSettings)]
        public OfferAutoscaleProperties OfferAutoscaleSettings { get; private set; }

        /// <summary>
        /// Represents Request Units(RU)/Minute throughput is enabled/disabled for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled)]
        public bool? OfferIsRUPerMinuteThroughputEnabled { get; private set; }

        /// <summary>
        /// Represents time stamp when offer was last replaced by user for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.OfferLastReplaceTimestamp)]
        internal long? OfferLastReplaceTimestamp { get; private set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }

        public static OfferContentProperties CreateManualOfferConent(int throughput)
        {
            return new OfferContentProperties(manualThroughput: throughput);
        }

        public static OfferContentProperties CreateAutoscaleOfferConent(
            int startingMaxThroughput,
            int? autoUpgradeMaxThroughputIncrementPercentage)
        {
            OfferAutoscaleProperties autoscaleProperties = new OfferAutoscaleProperties(
                startingMaxThroughput,
                autoUpgradeMaxThroughputIncrementPercentage);
            return new OfferContentProperties(autoscaleProperties);
        }
    }
}
