//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the offer for a resource (collection) in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Currently, offers are only bound to the collection resource.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Offer : Resource
    {
        /// <summary>
        /// Initializes a Resource offer for the Azure Cosmos DB service.
        /// </summary>
        public Offer()
        {
            this.OfferVersion = Constants.Offers.OfferVersion_V1;
        }

        /// <summary>
        /// Initializes a Resource offer from another offer object for the Azure Cosmos DB service.
        /// </summary>
        public Offer(Offer offer)
            : base(offer)
        {
            this.OfferVersion = Constants.Offers.OfferVersion_V1;
            this.ResourceLink = offer.ResourceLink;
            this.OfferType = offer.OfferType;
            this.OfferResourceId = offer.OfferResourceId;
        }

        /// <summary>
        /// Gets or sets the version of this offer resource in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferVersion, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string OfferVersion
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.OfferVersion);
            }
            internal set
            {
                base.SetValue(Constants.Properties.OfferVersion, value);
            }
        }

        /// <summary>
        /// Gets or sets the self-link of a resource to which the resource offer applies to in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ResourceLink)]
        public string ResourceLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ResourceLink);
            }
            internal set
            {
                base.SetValue(Constants.Properties.ResourceLink, value);
            }
        }

        /// <summary>
        /// Gets or sets the OfferType for the resource offer in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferType, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string OfferType
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.OfferType);
            }
            set
            {
                base.SetValue(Constants.Properties.OfferType, value);
            }
        }

        /// <summary>
        /// Gets or sets the Id of the resource on which the Offer applies to in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferResourceId)]
        internal string OfferResourceId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.OfferResourceId);
            }
            set
            {
                base.SetValue(Constants.Properties.OfferResourceId, value);
            }
        }

        /// <summary>
        /// Validates the property, by calling it, in case of any errors exception is thrown
        /// </summary>
        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.OfferVersion);
            base.GetValue<string>(Constants.Properties.ResourceLink);
            base.GetValue<string>(Constants.Properties.OfferType);
        }

        /// <summary>
        /// Compares the offer object with the current offer
        /// </summary>
        /// <param name="offer"></param>
        /// <returns>Boolean representing the equality result</returns>
        public bool Equals(Offer offer)
        {
            if (!this.OfferVersion.Equals(offer.OfferVersion) || !this.OfferResourceId.Equals(offer.OfferResourceId))
            {
                return false;
            }

            if (this.OfferVersion.Equals(Constants.Offers.OfferVersion_V1) && !this.OfferType.Equals(offer.OfferType))
            {
                return false;
            }

            return true;
        }
    }
}
