// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class OfferContentProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        private OfferContentProperties()
        {
        }

        private OfferContentProperties(int manualThroughput)
        {
            this.OfferThroughput = manualThroughput;
            this.OfferAutoscaleSettings = null;
        }

        private OfferContentProperties(OfferAutoscaleProperties autoscaleProperties)
        {
            this.OfferThroughput = null;
            this.OfferAutoscaleSettings = autoscaleProperties ?? throw new ArgumentNullException(nameof(autoscaleProperties));
        }

        /// <summary>
        /// Represents customizable throughput chosen by user for his collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferThroughput, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? OfferThroughput { get; private set; }

        /// <summary>
        /// Represents customizable throughput chosen by user for his collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AutopilotSettings, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public OfferAutoscaleProperties OfferAutoscaleSettings { get; private set; }

        /// <summary>
        /// Represents Request Units(RU)/Minute throughput is enabled/disabled for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferIsRUPerMinuteThroughputEnabled, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? OfferIsRUPerMinuteThroughputEnabled { get; private set; }

        /// <summary>
        /// Represents time stamp when offer was last replaced by user for collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferLastReplaceTimestamp, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal long? OfferLastReplaceTimestamp { get; private set; }

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
