//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class SerializableNameValueCollection : JsonSerializable
    {
        private Lazy<NameValueCollection> lazyCollection;

        public SerializableNameValueCollection()
        {
            this.lazyCollection = new Lazy<NameValueCollection>(this.Init);
        }

        public SerializableNameValueCollection(NameValueCollection collection)
        {
            this.lazyCollection = new Lazy<NameValueCollection>(this.Init);
            this.Collection.Add(collection);
        }

        [JsonIgnore]
        public NameValueCollection Collection
        {
            get
            {
                return this.lazyCollection.Value;
            }
        }

        // Helper methods
        public static string SaveToString(SerializableNameValueCollection nameValueCollection)
        {
            if (nameValueCollection == null)
            {
                return string.Empty;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                nameValueCollection.SaveTo(ms);
                ms.Position = 0;
                using (StreamReader reader = new StreamReader(ms))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static SerializableNameValueCollection LoadFromString(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(ms))
                    {
                        writer.Write(value);
                        writer.Flush();
                        ms.Position = 0;
                        return JsonSerializable.LoadFrom<SerializableNameValueCollection>(ms);
                    }
                }
            }

            return new SerializableNameValueCollection();
        }

        internal override void OnSave()
        {
            foreach (string key in this.Collection)
            {
                base.SetValue(key, this.Collection[key]);
            }
        }

        private NameValueCollection Init()
        {
            NameValueCollection collection = new NameValueCollection();
            if (this.propertyBag != null)
            {
                foreach (KeyValuePair<string, JToken> pair in this.propertyBag)
                {
                    if (pair.Value is JValue value)
                    {
                        collection.Add(pair.Key, value.ToString());
                    }
                }
            }

            return collection;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SerializableNameValueCollection);
        }

        public bool Equals(SerializableNameValueCollection collection)
        {
            if (collection is null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, collection))
            {
                return true;
            }

            return this.IsEqual(collection);
        }

        private bool IsEqual(SerializableNameValueCollection serializableNameValueCollection)
        {
            if (this.Collection.Count != serializableNameValueCollection.Collection.Count)
            {
                return false;
            }
            else
            {
                foreach (string key in this.Collection.AllKeys)
                {
                    if (this.Collection[key] != serializableNameValueCollection.Collection[key])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 0;
                foreach (string key in this.Collection)
                {
                    hash = (hash * 397) ^ key.GetHashCode();
                    hash = (hash * 397) ^ (this.Collection.Get(key) != null ? this.Collection.Get(key).GetHashCode() : 0);
                }

                return hash;
            }
        }
    }
}