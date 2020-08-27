//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    /// <summary>
    /// Represents the unique key policy configuration for specifying uniqueness constraints on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// var collectionSpec = new DocumentCollection
    /// {
    ///     Id = "Collection with unique keys",
    ///     UniqueKeyPolicy = new UniqueKeyPolicy
    ///     {
    ///         UniqueKeys = new Collection<UniqueKey> {
    ///             // pair </name/first, name/last> is unique.
    ///             new UniqueKey { Paths = new Collection<string> { "/name/first", "/name/last" } },
    ///             // /address is unique.
    ///             new UniqueKey { Paths = new Collection<string> { "/address" } },
    ///         }
    ///     }
    /// };
    /// DocumentCollection collection = await client.CreateDocumentCollectionAsync(databaseLink, collectionSpec });
    ///
    /// var doc = JObject.Parse("{\"name\": { \"first\": \"John\", \"last\": \"Smith\" }, \"alias\":\"johnsmith\" }");
    /// await client.CreateDocumentAsync(collection.SelfLink, doc);
    ///
    /// doc = JObject.Parse("{\"name\": { \"first\": \"James\", \"last\": \"Smith\" }, \"alias\":\"jamessmith\" }");
    /// await client.CreateDocumentAsync(collection.SelfLink, doc);
    ///
    /// try
    /// {
    ///     // Error: first+last name is not unique.
    ///     doc = JObject.Parse("{\"name\": { \"first\": \"John\", \"last\": \"Smith\" }, \"alias\":\"johnsmith1\" }");
    ///     await client.CreateDocumentAsync(collection.SelfLink, doc);
    ///     throw new Exception("CreateDocumentAsync should have thrown exception/conflict");
    /// }
    /// catch (DocumentClientException ex)
    /// {
    ///     if (ex.StatusCode != System.Net.HttpStatusCode.Conflict) throw;
    /// }
    ///
    /// try
    /// {
    ///     // Error: alias is not unique.
    ///     doc = JObject.Parse("{\"name\": { \"first\": \"James Jr\", \"last\": \"Smith\" }, \"alias\":\"jamessmith\" }");
    ///     await client.CreateDocumentAsync(collection.SelfLink, doc);
    ///     throw new Exception("CreateDocumentAsync should have thrown exception/conflict");
    /// }
    /// catch (DocumentClientException ex)
    /// {
    ///     if (ex.StatusCode != System.Net.HttpStatusCode.Conflict) throw;
    /// }
    /// ]]>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class UniqueKeyPolicy : JsonSerializable
    {
        private Collection<UniqueKey> uniqueKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueKeyPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public UniqueKeyPolicy()
        {
        }

        /// <summary>
        /// Gets or sets collection of <see cref="UniqueKey"/> that guarantee uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UniqueKeys)]
        public Collection<UniqueKey> UniqueKeys
        {
            get
            {
                if (this.uniqueKeys == null)
                {
                    this.uniqueKeys = base.GetValue<Collection<UniqueKey>>(Constants.Properties.UniqueKeys);
                    if (this.uniqueKeys == null)
                    {
                        this.uniqueKeys = new Collection<UniqueKey>();
                    }
                }

                return this.uniqueKeys;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "UniqueKeys"));
                }

                this.uniqueKeys = value;
                base.SetValue(Constants.Properties.UniqueKeys, this.uniqueKeys);
            }
        }

        internal override void OnSave()
        {
            if (this.uniqueKeys != null)
            {
                foreach (UniqueKey uniqueKey in this.uniqueKeys)
                {
                    uniqueKey.OnSave();
                }

                base.SetValue(Constants.Properties.UniqueKeys, this.uniqueKeys);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            foreach (UniqueKey uniqueKey in this.UniqueKeys)
            {
                uniqueKey.Validate();
            }
        }
    }
}
