//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    internal sealed class FeedResource<T> : Resource, IEnumerable<T> where T : JsonSerializable, new()
    {
        private static string collectionName;

        private static string CollectionName
        {
            get
            {
                if (FeedResource<T>.collectionName == null)
                {
                    if (typeof(Document).IsAssignableFrom(typeof(T)))
                    {
                        FeedResource<T>.collectionName = "Documents";
                    }
                    else if (typeof(Attachment).IsAssignableFrom(typeof(T)))
                    {
                        FeedResource<T>.collectionName = "Attachments";
                    }
                    else
                    {
                        FeedResource<T>.collectionName = typeof(T).Name + "s";
                    }
                }
                return FeedResource<T>.collectionName;
            }
        }

        public FeedResource()
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

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.InnerCollection.GetEnumerator();
        }

        internal Collection<T> InnerCollection
        {
            get
            {
                Collection<T> collection = base.GetObjectCollection<T>(FeedResource<T>.CollectionName, typeof(T), this.AltLink);
                if (collection == null)
                {
                    collection = new Collection<T>();
                    base.SetObjectCollection(FeedResource<T>.CollectionName, collection);
                }
                return collection;
            }
            set
            {
                base.SetObjectCollection(FeedResource<T>.CollectionName, value);
            }
        }

        internal override void OnSave()
        {
            base.SetValue(Constants.Properties.Count, this.InnerCollection.Count);
        }
    }
}
