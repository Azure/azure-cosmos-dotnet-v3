//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the Standard pricing offer for a resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Currently, offers are only bound to the collection resource.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class OfferV2 : Offer
    {
        /// <summary>
        /// Initializes a Resource offer with the Standard pricing tier for the Azure Cosmos DB service.
        /// </summary>
        internal OfferV2()
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;
        }

        /// <summary>
        /// Initializes a Resource offer with the Standard pricing tier for the Azure Cosmos DB service.
        /// </summary>
        public OfferV2(int offerThroughput)
            : this()
        {
            this.Content = new OfferContentV2(offerThroughput);
        }

        /// <summary>
        /// Initializes a Resource offer with the Standard pricing tier for the Azure Cosmos DB service.
        /// </summary>
        public OfferV2(int offerThroughput, bool? offerEnableRUPerMinuteThroughput)
            : this()
        {
            this.Content = new OfferContentV2(offerThroughput, offerEnableRUPerMinuteThroughput);
        }

        /// <summary>
        /// Initializes a Resource offer with the Standard pricing tier, from a reference Offer object for the Azure Cosmos DB service.
        /// </summary>
        public OfferV2(Offer offer, int offerThroughput)
            : base(offer)
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;

            OfferContentV2 contentV2 = null;
            if (offer is OfferV2)
            {
                contentV2 = ((OfferV2)offer).Content;
            }

            this.Content = new OfferContentV2(contentV2, offerThroughput, null);
        }

        /// <summary>
        /// Initializes a Resource offer with the Standard pricing tier, from a reference Offer object for the Azure Cosmos DB service.
        /// </summary>
        public OfferV2(Offer offer, int offerThroughput, bool? offerEnableRUPerMinuteThroughput)
            : base(offer)
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;

            OfferContentV2 contentV2 = null;
            if (offer is OfferV2)
            {
                contentV2 = ((OfferV2)offer).Content;
            }

            this.Content = new OfferContentV2(contentV2, offerThroughput, offerEnableRUPerMinuteThroughput);
        }

#if !DOCDBCLIENT
        /// <summary>
        /// Initializes a Resource offer with the given autopilot settings, from a reference Offer object for the Azue Cosmos DB service.
        /// </summary>
        internal OfferV2(Offer offer, AutopilotSettings autopilotSettings)
            : base(offer)
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;
            this.Content = new OfferContentV2(autopilotSettings);
        }

        /// <summary>
        /// Initializes a Resource offer with the given autopilot settings for the Azue Cosmos DB service.
        /// </summary>
        internal OfferV2(AutopilotSettings autopilotSettings)
            : this()
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;
            this.Content = new OfferContentV2(autopilotSettings);
        }

        /// <summary>
        /// Internal constructor initializes offer with the given throughput and autopilot settings.
        /// </summary>
        internal OfferV2(int offerThroughput,
                         bool? offerEnableRUPerMinuteThroughput,
                         bool? offerIsAutoScaleV1Enabled,
                         AutopilotSettings autopilotSettings)
            : this()
        {
            this.Content = new OfferContentV2(offerThroughput,
                                              offerEnableRUPerMinuteThroughput,
                                              offerIsAutoScaleV1Enabled,
                                              autopilotSettings,
                                              null);
        }

        /// <summary>
        /// Internal constructor that initializes offer with the given throughput, autoscale setting and from reference offer object
        /// </summary>
        internal OfferV2(Offer offer,
                         int offerThroughput,
                         bool? offerEnableRUPerMinuteThroughput,
                         bool? offerIsAutoScaleV1Enabled,
                         AutopilotSettings autopilotSettings,
                         OfferMinimumThroughputParameters minimumThoughputParameters)
            : base(offer)
        {
            this.OfferType = string.Empty;
            this.OfferVersion = Constants.Offers.OfferVersion_V2;

            this.Content = new OfferContentV2(offerThroughput,
                                              offerEnableRUPerMinuteThroughput,
                                              offerIsAutoScaleV1Enabled,
                                              autopilotSettings,
                                              minimumThoughputParameters);
        }
#endif

        /// <summary>
        /// Gets or sets the OfferContent for the resource offer in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferContent, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public OfferContentV2 Content
        {
            get
            {
                return base.GetObject<OfferContentV2>(Constants.Properties.OfferContent);
            }
            internal set
            {
                base.SetObject<OfferContentV2>(Constants.Properties.OfferContent, value);
            }
        }

        /// <summary>
        /// Validates the property, by calling it, in case of any errors exception is thrown
        /// </summary>
        internal override void Validate()
        {
            base.Validate();
            this.Content?.Validate();
        }

        /// <summary>
        /// Compares the offer object with the current offer
        /// </summary>
        /// <param name="offer"></param>
        /// <returns>Boolean representing the equality result</returns>
        public bool Equals(OfferV2 offer)
        {
            if (offer == null)
            {
                return false;
            }

            if (!base.Equals(offer))
            {
                return false;
            }

            if (this.Content == null && offer.Content == null)
            {
                return true;
            }
            else if (this.Content != null && offer.Content != null)
            {
#if DOCDBCLIENT
                return (this.Content.OfferThroughput == offer.Content.OfferThroughput) &&
                       (this.Content.OfferIsRUPerMinuteThroughputEnabled == offer.Content.OfferIsRUPerMinuteThroughputEnabled);
#else
                return (this.GetOfferThroughput(false) == offer.GetOfferThroughput(false)) &&
                       (this.Content.OfferIsRUPerMinuteThroughputEnabled == offer.Content.OfferIsRUPerMinuteThroughputEnabled) &&
                       // Unset or false should be treated the same. In gateway, if offer replace request to store times out we wait a bit 
                       // and read again from master and compare it to see if it is what we expect. If they are equal we treat it as success.
                       (this.Content.OfferIsAutoScaleEnabled.GetValueOrDefault(false) == offer.Content.OfferIsAutoScaleEnabled.GetValueOrDefault(false));
#endif
            }

            return false;
        }

#if !DOCDBCLIENT
        internal bool IsAutoScaleEnabled()
        {
            if (this.Content != null)
            {
                AutopilotSettings autopilotSettings = this.Content.OfferAutopilotSettings;

                // Autoscale V2: uses AutopilotSettings and takes precedencde over autoscale V1 settings
                if (autopilotSettings != null)
                {
                    // Presence of AutopilotSettings indicates Autopilot is enabled.
                    return true;
                }

                // Autoscale preview: uses OfferIsAutoScaleEnabled property
                if (this.Content.OfferIsAutoScaleEnabled.GetValueOrDefault(false))
                {
                    return true;
                }
            }

            return false;
        }

        internal int? GetOfferThroughput(bool isAutoScaleTriggeredRequest)
        {
            if (this.Content == null)
            {
                return null;
            }

            int? offerThroughput = this.Content.OfferThroughput;

            if (!isAutoScaleTriggeredRequest)
            {
                AutopilotSettings autopilotSettings = this.Content.OfferAutopilotSettings;
                if (autopilotSettings != null)
                {
                    offerThroughput = 0; // In case of autopilot request, Throughput is based on the Tier.
                }
                else if(this.Content.OfferIsAutoScaleEnabled.GetValueOrDefault(false))
                {
                    if(this.Content.CollectionThroughputInfo.UserSpecifiedThroughput.HasValue)
                    {
                        offerThroughput = this.Content.CollectionThroughputInfo.UserSpecifiedThroughput.Value;
                    }
                }
            }

            return offerThroughput;
        }
#endif
    }
}
