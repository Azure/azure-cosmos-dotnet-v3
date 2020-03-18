//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos
{
    using System.Collections.ObjectModel;

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
    public sealed class UniqueKeyPolicy
    {
        /// <summary>
        /// Gets collection of <see cref="UniqueKey"/> that guarantee uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        public Collection<UniqueKey> UniqueKeys { get; internal set; } = new Collection<UniqueKey>();
    }
}
