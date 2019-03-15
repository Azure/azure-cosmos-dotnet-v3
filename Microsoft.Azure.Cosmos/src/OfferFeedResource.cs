//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class OfferFeedResource : CosmosResource, IEnumerable<Offer>
    {
        private static string CollectionName
        {
            get
            {
                return typeof(Offer).Name + "s";
            }
        }

        public OfferFeedResource()
        {

        }        
        
        public int Count
        {
            get
            {
                return this.InnerCollection.Count;
            }            
        }        
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.InnerCollection.GetEnumerator();
        }

        IEnumerator<Offer> IEnumerable<Offer>.GetEnumerator()
        {
            return this.InnerCollection.GetEnumerator();
        }

        internal Collection<Offer> InnerCollection
        {
            get
            {
                Collection<Offer> collection = this.GetObjectCollection<Offer>(
                    OfferFeedResource.CollectionName, 
                    typeof(Offer), 
                    this.AltLink, 
                    OfferTypeResolver.ResponseOfferTypeResolver);
                if (collection == null)
                {
                    collection = new Collection<Offer>();
                    base.SetObjectCollection(OfferFeedResource.CollectionName, collection);
                }
                return collection;
            }
            set
            {
                base.SetObjectCollection(OfferFeedResource.CollectionName, value);
            }
        }

        internal override void OnSave()
        {
            base.SetValue(Constants.Properties.Count, this.InnerCollection.Count);
        }
    }
}
