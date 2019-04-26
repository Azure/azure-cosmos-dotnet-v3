//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents a unique key on that enforces uniqueness constraint on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// 1) For partitioned collections, the value of partition key is implicitly a part of each unique key.
    /// 2) Uniqueness constraint is also enforced for missing values.
    /// For instance, if unique key policy defines a unique key with single property path, there could be only one document that has missing value for this property.
    /// </remarks>
    /// <seealso cref="UniqueKeyPolicy"/>
    public sealed class UniqueKey 
    {
        /// <summary>
        /// Gets or sets the paths, a set of which must be unique for each document in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <![CDATA[The paths to enforce uniqueness on. Each path is a rooted path of the unique property in the document, such as "/name/first".]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// uniqueKey.Paths = new Collection<string> { "/name/first", "/name/last" };
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.Paths)]
        public Collection<string> Paths { get; set; } = new Collection<string>();
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class UniqueueKeyBuilder
    {
        private Collection<string> Paths { get; set; } = new Collection<string>();
        private CosmosContainerBuilder Root { get; }

        internal UniqueueKeyBuilder(CosmosContainerBuilder root)
        {
            this.Root = root;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public UniqueueKeyBuilder Path(string name)
        {
            this.Paths.Add(name);
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public CosmosContainerBuilder Attach()
        {
            return this.Root;
        }
    }
}
