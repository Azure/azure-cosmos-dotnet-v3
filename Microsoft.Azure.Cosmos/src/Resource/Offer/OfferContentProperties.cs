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

        private OfferContentProperties(int fixedThroughput)
        {
            this.OfferThroughput = fixedThroughput;
            this.OfferAutopilotSettings = null;
        }

        private OfferContentProperties(OfferAutopilotProperties autopilotProperties)
        {
            this.OfferThroughput = null;
            this.OfferAutopilotSettings = autopilotProperties ?? throw new ArgumentNullException(nameof(autopilotProperties));
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
        public OfferAutopilotProperties OfferAutopilotSettings { get; private set; }

        public static OfferContentProperties CreateFixedOfferConent(int throughput)
        {
            return new OfferContentProperties(fixedThroughput: throughput);
        }

        public static OfferContentProperties CreateAutoPilotOfferConent(int maxThroughput)
        {
            OfferAutopilotProperties autopilotProperties = new OfferAutopilotProperties(maxThroughput);
            return new OfferContentProperties(autopilotProperties);
        }

        public static OfferContentProperties CreateAutoPilotOfferConent(
            int startingMaxThroughput,
            int autoUpgradeMaxThroughputIncrementPercentage)
        {
            OfferAutopilotProperties autopilotProperties = new OfferAutopilotProperties(
                startingMaxThroughput,
                autoUpgradeMaxThroughputIncrementPercentage);
            return new OfferContentProperties(autopilotProperties);
        }
    }
}
