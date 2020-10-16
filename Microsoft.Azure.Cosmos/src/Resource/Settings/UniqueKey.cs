//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------



namespace Microsoft.Azure.Cosmos
{
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a unique key on that enforces uniqueness constraint on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// 1) For partitioned collections, the value of partition key is implicitly a part of each unique key.
    /// 2) Uniqueness constraint is also enforced for missing values.
    /// For instance, if unique key policy defines a unique key with single property path, there could be only one document that has missing value for this property.
    /// </remarks>
    /// <seealso cref="UniqueKeyPolicy"/>
    public class UniqueKey
    {
        [JsonProperty(PropertyName = Constants.Properties.Filter, NullValueHandling = NullValueHandling.Ignore)]
        private SqlQuerySpec querySpec;

        /// <summary>
        /// Gets the paths, a set of which must be unique for each document in the Azure Cosmos DB service.
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
        public Collection<string> Paths { get; internal set; } = new Collection<string>();

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public QueryDefinition Filter {
            get => this.querySpec == null ? null : new QueryDefinition(this.querySpec);
            set => this.querySpec = value.ToSqlQuerySpec();
        }
    }
}
