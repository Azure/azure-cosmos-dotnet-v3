//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Offer resolver based on input.
    /// </summary>
    internal sealed class OfferTypeResolver : ITypeResolver<Offer>
    {
        public static readonly ITypeResolver<Offer> RequestOfferTypeResolver = new OfferTypeResolver(false);
        public static readonly ITypeResolver<Offer> ResponseOfferTypeResolver = new OfferTypeResolver(true);

        private readonly bool isResponse;

        /// <summary>
        /// Constructor with a flag indicating whether this is invoked in response or request path.
        /// </summary>
        /// <param name="isResponse">True if invoked in response path</param>
        private OfferTypeResolver(bool isResponse)
        {
            this.isResponse = isResponse;
        }

        /// <summary>
        /// Returns a reference of an object in Offer's hierarchy based on a property bag.
        /// </summary>
        /// <param name="propertyBag">Property bag used to deserialize Offer object</param>
        /// <returns>Object of type Offer or OfferV2</returns>
        Offer ITypeResolver<Offer>.Resolve(JObject propertyBag)
        {
            Offer resource;
            if (propertyBag != null)
            {
                resource = new Offer();
                resource.propertyBag = propertyBag;

                switch (resource.OfferVersion ?? String.Empty)
                {
                    case Constants.Offers.OfferVersion_V1:
                    case Constants.Offers.OfferVersion_None:
                        break;

                    case Constants.Offers.OfferVersion_V2:
                        {
                            resource = new OfferV2();
                            resource.propertyBag = propertyBag; // convert Offer resource to V2.
                        }
                        break;

                    default:
                        {
                            DefaultTrace.TraceCritical("Unexpected offer version {0}", resource.OfferVersion);                            
                            if(!isResponse)
                            {
                                throw new BadRequestException(string.Format(CultureInfo.CurrentUICulture, RMResources.UnsupportedOfferVersion, resource.OfferVersion));
                            }
                            
                            // in case we get unrecognized offer version from server, we return default Offer.
                        }
                        break;
                }
            }
            else
            {
                resource = default(Offer);
            }

            return resource;
        }
    }
}
