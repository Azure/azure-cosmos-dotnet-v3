//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the unique key policy configuration for specifying uniqueness constraints on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Unique key policies add a layer of data integrity to an Azure Cosmos container. They cannot be modified once the container is created.
    /// <para>
    /// Refer to <see>https://docs.microsoft.com/en-us/azure/cosmos-db/unique-keys</see> for additional information on how to specify
    /// unique key policies.
    /// </para>
    /// </remarks>
    /// <seealso cref="ContainerProperties"/>
    public sealed class UniqueKeyPolicy
    {
        /// <summary>
        /// Gets collection of <see cref="UniqueKey"/> that guarantee uniqueness of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UniqueKeys)]
        public Collection<UniqueKey> UniqueKeys { get; internal set; } = new Collection<UniqueKey>();
    }
}
